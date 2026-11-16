using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace RDownloader.Native
{
    // Lifetime of the program: one instance only, tray icon, "keep running in the background when the window is closed",
    // start with Windows (scheduled task at logon) and the command-line modes the installer uses.
    internal static class AppLife
    {
        public const string StartupTask = "R-Downloader Startup";
        public static bool Quitting;
        public static event Action QuitEvent;

        private static Mutex _mutex;
        private static EventWaitHandle _showEvt, _quitEvt, _openEvt;
        public static bool RlistMode;
        private static System.Windows.Forms.NotifyIcon _tray;
        private static System.Drawing.Icon _icon;
        private static System.Windows.Forms.ToolStripMenuItem _startupItem;

        public static string ExePath { get { return Assembly.GetExecutingAssembly().Location; } }
        private static string User { get { return Environment.UserDomainName + "\\" + Environment.UserName; } }

        private static void Post(Action a)
        {
            Application app = Application.Current;
            if (app != null) app.Dispatcher.BeginInvoke(a);
        }

        // ---------------- command line modes (no window) ----------------
        public static bool HandleCommand(string[] args, out int code)
        {
            code = 0;
            foreach (string a in args)
            {
                switch (a.ToLowerInvariant())
                {
                    case "--quit":
                        try { EventWaitHandle.OpenExisting(@"Local\RDownloader_Quit").Set(); } catch { }
                        return true;
                    case "--enable-startup":
                        try { TaskSched.CreateLogon(StartupTask, ExePath, "--background", User); } catch { code = 1; }
                        return true;
                    case "--disable-startup":
                        TaskSched.Delete(StartupTask);
                        return true;
                    case "--pin-taskbar":
                        try { PinTaskbar(true); } catch { code = 1; }
                        return true;
                    case "--unpin-taskbar":
                        try { UnpinTaskbar(false); } catch { code = 1; }
                        return true;
                }
            }
            return false;
        }

        // ---------------- single instance ----------------
        public static bool EnsureSingleInstance(bool restarted, System.Collections.Generic.List<string> rlists)
        {
            bool created;
            _mutex = new Mutex(true, @"Local\RDownloader_Instance", out created);
            _showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\RDownloader_Show");
            _openEvt = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\RDownloader_Open");
            if (!created && restarted)
            {
                // the previous window is closing (theme / language change): wait for it
                try { created = _mutex.WaitOne(10000); } catch (AbandonedMutexException) { created = true; }
            }
            if (!created)
            {
                if (rlists != null && rlists.Count > 0)
                {
                    // a .Rlist file was opened: the running copy shows only the Rlist window
                    foreach (string p in rlists) Rlist.Enqueue(p);
                    try { _openEvt.Set(); } catch { }
                }
                else { try { _showEvt.Set(); } catch { } }   // ask the running copy to come to the front
                return false;
            }
            _quitEvt = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\RDownloader_Quit");
            Thread t = new Thread(Listen);
            t.IsBackground = true;
            t.Start();
            return true;
        }

        private static void Listen()
        {
            WaitHandle[] h = new WaitHandle[] { _showEvt, _quitEvt, _openEvt };
            while (true)
            {
                int i = WaitHandle.WaitAny(h);
                if (Quitting) return;
                if (i == 0) Post(ShowWindow); else if (i == 2) Post(OpenQueued); else Post(Exit);
            }
        }

        // ---------------- window / tray ----------------
        private static bool _sessionHooked;
        private static Window _replaced;

        // the main window was rebuilt (look / language change): close the old one for real instead of hiding it to the tray
        public static void ReplaceWindow(Window old)
        {
            _replaced = old;
            try { old.Close(); } catch { }
            _replaced = null;
        }

        public static void RefreshTray()
        {
            try
            {
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
                if (_icon != null) { _icon.Dispose(); _icon = null; }
            }
            catch { }
            InitTray();
        }

        public static void AttachWindow(Window w)
        {
            w.Closing += delegate(object s, CancelEventArgs e)
            {
                if (Quitting || object.ReferenceEquals(w, _replaced)) return;
                e.Cancel = true;
                if (Ui.Settings.GetBool("CloseToTray", true)) w.Hide();
                else Exit();
            };
            if (!_sessionHooked) { _sessionHooked = true; Application.Current.SessionEnding += delegate { Quitting = true; RaiseQuit(); }; }
        }

        // .Rlist files handed over by another copy of the program
        public static void OpenQueued()
        {
            foreach (string p in Rlist.TakeQueue()) OpenItem(p);
        }

        // a .Rlist file opens its own window; a magnet link or .torrent file is added to the download list
        public static bool IsTorrentItem(string p) { return !string.IsNullOrEmpty(p) && (p.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)); }
        public static void OpenItem(string p)
        {
            try
            {
                if (IsTorrentItem(p)) { RlistMode = false; ShowWindow(); TorrentDlg.AddLink(p); }
                else DlWin.OpenRlist(p);
            }
            catch (Exception ex) { Ui.Log.Warn("Could not open " + p + ": " + ex.Message, "Downloads"); }
        }

        // started only to show a .Rlist file: stay invisible and leave when the Rlist window is closed and nothing is downloading
        public static void StartRlistWatch()
        {
            RlistMode = true;
            System.Windows.Threading.DispatcherTimer t = new System.Windows.Threading.DispatcherTimer();
            t.Interval = TimeSpan.FromSeconds(2);
            t.Tick += delegate
            {
                if (!RlistMode) { t.Stop(); return; }
                DownloadsPage dp = DownloadsPage.Instance;
                bool busy = dp != null && dp.Busy;
                if (!DlWin.AnyOpen && !busy && (Ui.Win == null || !Ui.Win.IsVisible)) { t.Stop(); Exit(); }
            };
            t.Start();
        }

        public static void ShowWindow()
        {
            RlistMode = false;
            Window w = Ui.Win;
            if (w == null) return;
            if (!w.IsVisible) w.Show();
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            w.Activate();
            bool top = w.Topmost; w.Topmost = true; w.Topmost = top;
            w.Focus();
        }

        public static void InitTray()
        {
            try { _icon = System.Drawing.Icon.ExtractAssociatedIcon(ExePath); } catch { }
            _tray = new System.Windows.Forms.NotifyIcon();
            if (_icon != null) _tray.Icon = _icon;
            _tray.Text = "R-Downloader";
            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
            if (Lang.Rtl) menu.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            System.Windows.Forms.ToolStripMenuItem open = new System.Windows.Forms.ToolStripMenuItem(Lang.L("Open R-Downloader"));
            open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
            open.Click += delegate { ShowWindow(); };
            _startupItem = new System.Windows.Forms.ToolStripMenuItem(Lang.L("Run at Windows startup"));
            _startupItem.Click += delegate
            {
                try { SetStartup(!StartupEnabled()); } catch (Exception ex) { Ui.Log.Error("Could not change startup: " + ex.Message, "Startup"); }
            };
            System.Windows.Forms.ToolStripMenuItem exit = new System.Windows.Forms.ToolStripMenuItem(Lang.L("Exit"));
            exit.Click += delegate { Exit(); };
            menu.Items.Add(open);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(_startupItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(exit);
            menu.Opening += delegate { _startupItem.Checked = StartupEnabled(); };
            try { TrayRenderer.Apply(menu); } catch { }
            _tray.ContextMenuStrip = menu;
            _tray.MouseClick += delegate(object s, System.Windows.Forms.MouseEventArgs e)
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left) ShowWindow();
            };
            _tray.Visible = true;
        }

        // opens a .Rlist file for the current copy
        public static System.Collections.Generic.List<string> RlistArgs(string[] args)
        {
            System.Collections.Generic.List<string> r = new System.Collections.Generic.List<string>();
            foreach (string s in args)
            {
                if (Rlist.IsRlistPath(s) || s.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    try { string full = Path.GetFullPath(s); if (File.Exists(full)) r.Add(full); } catch { }
                }
                else if (s.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)) r.Add(s);
            }
            return r;
        }

        // ---------------- start with Windows ----------------
        public static bool StartupEnabled() { return TaskSched.Exists(StartupTask); }

        public static void SetStartup(bool on)
        {
            if (on) TaskSched.CreateLogon(StartupTask, ExePath, "--background", User);
            else TaskSched.Delete(StartupTask);
        }

        // ---------------- taskbar pin (shortcut in the pinned-items folder) ----------------
        private static string TaskbarDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"); }
        }

        public static System.Collections.Generic.List<string> PinnedLinks()
        {
            System.Collections.Generic.List<string> res = new System.Collections.Generic.List<string>();
            try
            {
                if (!Directory.Exists(TaskbarDir)) return res;
                dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                foreach (string f in Directory.GetFiles(TaskbarDir, "*.lnk"))
                {
                    bool hit = false;
                    try { dynamic l = sh.CreateShortcut(f); hit = string.Equals(Convert.ToString(l.TargetPath), ExePath, StringComparison.OrdinalIgnoreCase); } catch { }
                    if (hit) res.Add(f);
                }
            }
            catch { }
            return res;
        }

        public static void PinTaskbar(bool restartExplorer)
        {
            if (PinnedLinks().Count > 0) return;
            Directory.CreateDirectory(TaskbarDir);
            dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic l = sh.CreateShortcut(Path.Combine(TaskbarDir, "R-Downloader.lnk"));
            l.TargetPath = ExePath;
            l.WorkingDirectory = Path.GetDirectoryName(ExePath);
            l.IconLocation = ExePath + ",0";
            l.Description = "R-Downloader";
            l.Save();
            if (restartExplorer) SysUtil.KillByName("explorer");
        }

        public static void UnpinTaskbar(bool restartExplorer)
        {
            foreach (string f in PinnedLinks()) { try { File.Delete(f); } catch { } }
            if (restartExplorer) SysUtil.KillByName("explorer");
        }

        // ---------------- exit / restart ----------------
        private static void RaiseQuit()
        {
            Action h = QuitEvent;
            if (h != null) { try { h(); } catch { } }
        }

        public static void Exit()
        {
            if (Quitting) return;
            Quitting = true;
            RaiseQuit();
            try { _showEvt.Set(); } catch { }
            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
            try { if (_icon != null) _icon.Dispose(); } catch { }
            try { Ui.Settings.SaveNow(); } catch { }
            Application.Current.Shutdown();
        }

        public static void Restart()
        {
            try { Ui.Settings.SaveNow(); } catch { }
            try { _mutex.ReleaseMutex(); } catch { }
            try { Process.Start(ExePath, "--restarted"); } catch { }
            Exit();
        }
    }
}
