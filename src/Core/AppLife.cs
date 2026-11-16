using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace RockToolbox.Native
{
    // Lifetime of the program: one instance only, tray icon, "keep running in the background when the window is closed",
    // start with Windows (scheduled task at logon) and the command-line modes the installer uses.
    internal static class AppLife
    {
        public const string StartupTask = "Rock Toolbox Startup";
        public static bool Quitting;
        public static event Action QuitEvent;

        private static Mutex _mutex;
        private static EventWaitHandle _showEvt, _quitEvt;
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
                        try { EventWaitHandle.OpenExisting(@"Local\RockToolbox_Quit").Set(); } catch { }
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
        public static bool EnsureSingleInstance(bool restarted)
        {
            bool created;
            _mutex = new Mutex(true, @"Local\RockToolbox_Instance", out created);
            _showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\RockToolbox_Show");
            if (!created && restarted)
            {
                // the previous window is closing (theme / language change): wait for it
                try { created = _mutex.WaitOne(10000); } catch (AbandonedMutexException) { created = true; }
            }
            if (!created)
            {
                try { _showEvt.Set(); } catch { }   // ask the running copy to come to the front
                return false;
            }
            _quitEvt = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\RockToolbox_Quit");
            Thread t = new Thread(Listen);
            t.IsBackground = true;
            t.Start();
            return true;
        }

        private static void Listen()
        {
            WaitHandle[] h = new WaitHandle[] { _showEvt, _quitEvt };
            while (true)
            {
                int i = WaitHandle.WaitAny(h);
                if (Quitting) return;
                if (i == 0) Post(ShowWindow); else Post(Exit);
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
                Exit();      // closing the window always quits Rock Toolbox (no background process); R-Downloader is a separate program
            };
            if (!_sessionHooked) { _sessionHooked = true; Application.Current.SessionEnding += delegate { Quitting = true; RaiseQuit(); }; }
        }

        public static void ShowWindow()
        {
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
            _tray.Text = "Rock Toolbox";
            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
            if (Lang.Rtl) menu.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            System.Windows.Forms.ToolStripMenuItem open = new System.Windows.Forms.ToolStripMenuItem(Lang.L("Open Rock Toolbox"));
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
            dynamic l = sh.CreateShortcut(Path.Combine(TaskbarDir, "Rock Toolbox.lnk"));
            l.TargetPath = ExePath;
            l.WorkingDirectory = Path.GetDirectoryName(ExePath);
            l.IconLocation = ExePath + ",0";
            l.Description = "Rock Toolbox";
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
