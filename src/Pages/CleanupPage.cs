using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RockToolbox.Native
{
    internal sealed class CleanItem : Notify
    {
        private bool _selected;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public bool Recommended { get; set; }
        public long Bytes { get; set; }
        public string Size { get; set; }
        public bool Selected { get { return _selected; } set { _selected = value; Raise("Selected"); } }
    }

    // C# port of src\pages\Cleanup.ps1
    internal sealed class CleanupPage : IPage
    {
        private sealed class Def
        {
            public string Id, Name, Desc; public bool Rec; public string[] Paths;
            public Def(string id, string name, bool rec, string desc, params string[] paths)
            { Id = id; Name = name; Rec = rec; Desc = desc; Paths = paths; }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        private static string E(string s) { return Environment.ExpandEnvironmentVariables(s); }

        private static readonly Def[] Defs = new Def[]
        {
            new Def("usertemp", "User temporary files", true, "Your %TEMP% folder - leftovers of installers and apps.", E(@"%TEMP%\*")),
            new Def("wintemp", "Windows temporary files", true, @"C:\Windows\Temp", E(@"%windir%\Temp\*")),
            new Def("wucache", "Windows Update download cache", true, "Already-downloaded update files (Windows re-downloads what it needs). Update services are paused while cleaning.", E(@"%windir%\SoftwareDistribution\Download\*")),
            new Def("delivopt", "Delivery Optimization cache", true, "Update files shared with other PCs. The Delivery Optimization service is paused while cleaning.", E(@"%windir%\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache\*"), E(@"%windir%\SoftwareDistribution\DeliveryOptimization\*")),
            new Def("wer", "Windows error reports", true, "Crash reports waiting to be sent.", E(@"%ProgramData%\Microsoft\Windows\WER\ReportArchive\*"), E(@"%ProgramData%\Microsoft\Windows\WER\ReportQueue\*"), E(@"%ProgramData%\Microsoft\Windows\WER\Temp\*")),
            new Def("dumps", "Crash dumps", true, "Memory dumps from blue screens (MEMORY.DMP, Minidump).", E(@"%windir%\Minidump\*"), E(@"%windir%\MEMORY.DMP"), E(@"%windir%\LiveKernelReports\*.dmp")),
            new Def("cbslogs", "Windows servicing logs", true, "CBS and DISM log files.", E(@"%windir%\Logs\CBS\*.log"), E(@"%windir%\Logs\CBS\*.cab"), E(@"%windir%\Logs\DISM\*.log")),
            new Def("thumbs", "Thumbnail cache", true, "Explorer rebuilds the picture previews when needed (Explorer restarts for a moment if it holds the files).", E(@"%LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*.db")),
            new Def("recycle", "Recycle Bin", false, "Permanently deletes everything in the Recycle Bin of all drives."),
            new Def("chrome", "Google Chrome cache", true, "Cached web pages (not passwords, history or bookmarks).", E(@"%LOCALAPPDATA%\Google\Chrome\User Data\*\Cache\*"), E(@"%LOCALAPPDATA%\Google\Chrome\User Data\*\Code Cache\*"), E(@"%LOCALAPPDATA%\Google\Chrome\User Data\*\GPUCache\*")),
            new Def("edge", "Microsoft Edge cache", true, "Cached web pages (not passwords, history or bookmarks).", E(@"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\Cache\*"), E(@"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\Code Cache\*"), E(@"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\GPUCache\*")),
            new Def("firefox", "Firefox cache", true, "Cached web pages (not passwords, history or bookmarks).", E(@"%LOCALAPPDATA%\Mozilla\Firefox\Profiles\*\cache2\*")),
            new Def("prefetch", "Prefetch files", false, "Windows rebuilds these, apps open a little slower the first time after cleaning.", E(@"%windir%\Prefetch\*.pf")),
            new Def("winold", "Previous Windows installation (Windows.old)", false, "Frees many GB but you can no longer roll back to the previous Windows version. Slow to delete.", E(@"%SystemDrive%\Windows.old"))
        };

        private sealed class ItemResult
        {
            public string Id, Name;
            public long Before, After, Freed;
            public int InUse, Denied, Other;
        }

        private ListView _lv;
        private TextBlock _count;
        private TextBlock _total;
        private TextBlock _result;
        private Border _resultBox;
        private readonly Dictionary<string, string> _last = new Dictionary<string, string>();

        public string Key { get { return "Cleanup"; } }

        public void Init(Window w, Logger log)
        {
            _lv = Ui.Find<ListView>("lvClean");
            _count = Ui.Find<TextBlock>("txtCleanCount");
            _total = Ui.Find<TextBlock>("txtCleanTotal");
            _result = Ui.Find<TextBlock>("txtCleanResult");
            _resultBox = Ui.Find<Border>("bdCleanResult");
            Ui.Find<Button>("btnCleanScan").Click += delegate { Scan(); };
            Ui.Find<Button>("btnCleanRec").Click += delegate { foreach (CleanItem x in Items()) x.Selected = x.Recommended && x.Bytes > 0; UpdateCount(); };
            Ui.Find<Button>("btnCleanAll").Click += delegate { foreach (CleanItem x in Items()) x.Selected = true; UpdateCount(); };
            Ui.Find<Button>("btnCleanNone").Click += delegate { foreach (CleanItem x in Items()) x.Selected = false; UpdateCount(); };
            _lv.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate { UpdateCount(); }));
            _lv.MouseDoubleClick += delegate
            {
                CleanItem it = _lv.SelectedItem as CleanItem;
                if (it != null) { it.Selected = !it.Selected; UpdateCount(); }
            };
            Ui.Find<Button>("btnCleanRun").Click += delegate { Run(); };
        }

        public void OnShown() { if (_lv.ItemsSource == null) Scan(); }

        private List<CleanItem> Items()
        {
            List<CleanItem> l = _lv.ItemsSource as List<CleanItem>;
            return l ?? new List<CleanItem>();
        }

        private void UpdateCount()
        {
            int n = 0; double sum = 0;
            foreach (CleanItem x in Items()) if (x.Selected) { n++; sum += x.Bytes; }
            _count.Text = "Selected: " + n + "  (about " + Fmt.Bytes(sum) + ")";
        }

        private static long Measure(Def d)
        {
            double b = 0;
            if (d.Id == "recycle")
            {
                foreach (DriveInfo dr in DriveInfo.GetDrives())
                    if (dr.DriveType == DriveType.Fixed) b += FastFs.DirSize(dr.Name + "$Recycle.Bin");
                return (long)b;
            }
            foreach (string p in d.Paths) b += FastFs.PatternSize(p);
            return (long)b;
        }

        private void DoScan(WorkCtx c)
        {
            c.Info("Measuring what can be cleaned...", "Cleanup");
            List<CleanItem> list = new List<CleanItem>();
            double total = 0;
            for (int i = 0; i < Defs.Length; i++)
            {
                c.ThrowIfCancelled();
                c.Progress(100.0 * i / Defs.Length, "Measuring: " + Defs[i].Name);
                CleanItem o = new CleanItem();
                o.Id = Defs[i].Id; o.Name = Defs[i].Name; o.Recommended = Defs[i].Rec;
                o.Desc = Defs[i].Desc;
                string note;
                if (_last.TryGetValue(Defs[i].Id, out note)) o.Desc = o.Desc + "   [" + note + "]";
                o.Bytes = Measure(Defs[i]);
                o.Size = Fmt.Bytes(o.Bytes);
                o.Selected = Defs[i].Rec && o.Bytes > 0;
                total += o.Bytes;
                list.Add(o);
            }
            string totalText = total > 0
                ? "About " + Fmt.Bytes(total) + " can be cleaned in total."
                : "Nothing to clean - these locations are empty.";
            c.UI(delegate
            {
                _lv.ItemsSource = list;
                UpdateCount();
                _total.Text = Lang.L(totalText);
                AnimateRows(list);
            });
            c.Info(totalText, "Cleanup");
        }

        // rows fade in one after another
        private void AnimateRows(List<CleanItem> list)
        {
            try
            {
                _lv.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(delegate
                {
                    try
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            ListViewItem li = _lv.ItemContainerGenerator.ContainerFromItem(list[i]) as ListViewItem;
                            if (li != null) Fx.FadeUp(li, 25 * i, 10);
                        }
                    }
                    catch (Exception) { }
                }));
            }
            catch (Exception) { }
        }

        private void Scan() { Work.Run("Measuring...", delegate(WorkCtx c) { DoScan(c); }); }

        private static bool IsAdmin()
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
            catch (Exception) { return false; }
        }

        private static bool SvcRunning(string name)
        {
            try { using (ServiceController sc = new ServiceController(name)) return sc.Status == ServiceControllerStatus.Running; }
            catch (Exception) { return false; }
        }

        private static void RunPs(WorkCtx c, string cmd)
        {
            try { c.Tool(Tool.System32(@"WindowsPowerShell\v1.0\powershell.exe"), "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + cmd + "\""); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { c.Warn("A PowerShell step was skipped: " + ex.Message, "Cleanup"); }
        }

        private static void Sweep(Def d, DelStats st, Action<long> onBytes)
        {
            foreach (string p in d.Paths) { if (Work.CancelRequested) break; FastFs.DeletePatternTracked(p, st, onBytes); }
        }

        // cleans one item with the method that really works for it, and reports the REAL result (size before / after)
        private static ItemResult CleanOne(WorkCtx c, Def d, double basePct, double span)
        {
            ItemResult r = new ItemResult();
            r.Id = d.Id; r.Name = d.Name;
            r.Before = Measure(d);
            if (r.Before == 0 && d.Id != "recycle") return r;

            long done = 0;
            long total = r.Before > 0 ? r.Before : 1;
            long lastTick = 0;
            Action<long> onBytes = delegate(long b)
            {
                long dn = Interlocked.Add(ref done, b);
                long now = Environment.TickCount;
                long prev = Interlocked.Read(ref lastTick);
                if (now - prev < 150) return;
                if (Interlocked.CompareExchange(ref lastTick, now, prev) != prev) return;
                double f = (double)dn / (double)total; if (f > 1) f = 1;
                c.Progress(basePct + span * 0.95 * f, "Cleaning: " + d.Name + " - " + Fmt.Bytes(dn) + " / " + Fmt.Bytes(total));
            };

            DelStats st = new DelStats();
            if (d.Id == "recycle")
            {
                int hr = SHEmptyRecycleBin(IntPtr.Zero, null, 7);
                if (hr < 0) c.Warn("The Recycle Bin could not be emptied (code 0x" + hr.ToString("X8") + ").", "Cleanup");
            }
            else if (d.Id == "winold")
            {
                c.Progress(-1, "Deleting " + d.Name + " - this can take several minutes");
                DeleteWindowsOld(c, d.Paths[0]);
            }
            else if (d.Id == "delivopt")
            {
                // official way first (the service must be running for it), then stop the service and sweep what is left
                RunPs(c, "Delete-DeliveryOptimizationCache -Force");
                bool was = SvcRunning("DoSvc");
                SysUtil.StopService("DoSvc");
                try
                {
                    Sweep(d, st, onBytes);
                    // files owned by the NetworkService account: take ownership and empty the folder with robocopy (fast, ignores ACL problems)
                    if (Measure(d) > 0)
                    {
                        c.Progress(-1, "Cleaning: " + d.Name + " - taking ownership of the cache folders");
                        foreach (string pth in d.Paths) ForceEmpty(c, pth);
                        st = new DelStats();
                        Sweep(d, st, null);
                    }
                }
                finally { if (was) SysUtil.StartService("DoSvc"); }
            }
            else if (d.Id == "wucache")
            {
                string[] names = new string[] { "wuauserv", "bits", "UsoSvc", "DoSvc" };
                List<string> stopped = new List<string>();
                foreach (string n in names) if (SvcRunning(n)) stopped.Add(n);
                foreach (string n in names) SysUtil.StopService(n);
                Thread.Sleep(500);
                try { Sweep(d, st, onBytes); }
                finally { for (int k = stopped.Count - 1; k >= 0; k--) SysUtil.StartService(stopped[k]); }
            }
            else if (d.Id == "thumbs")
            {
                Sweep(d, st, onBytes);
                if (st.Failed > 0)
                {
                    // Explorer holds the thumbnail database: close it, delete, let Windows start it again
                    SysUtil.KillByName("explorer");
                    Thread.Sleep(800);
                    st = new DelStats();
                    Sweep(d, st, onBytes);
                    Thread.Sleep(2500);
                    try
                    {
                        if (Process.GetProcessesByName("explorer").Length == 0)
                            Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
                    }
                    catch (Exception) { }
                }
            }
            else Sweep(d, st, onBytes);

            r.After = Measure(d);
            r.Freed = r.Before - r.After; if (r.Freed < 0) r.Freed = 0;
            r.InUse = st.InUse; r.Denied = st.Denied; r.Other = st.Other;
            return r;
        }

        private static string Reasons(int inUse, int denied, int other)
        {
            List<string> p = new List<string>();
            if (inUse > 0) p.Add(inUse + " " + Lang.L("in use"));
            if (denied > 0) p.Add(denied + " " + Lang.L("access denied"));
            if (other > 0) p.Add(other + " " + Lang.L("other errors"));
            return string.Join(", ", p.ToArray());
        }

        private void Run()
        {
            List<CleanItem> sel = new List<CleanItem>();
            foreach (CleanItem x in Items()) if (x.Selected) sel.Add(x);
            if (sel.Count == 0) { Dlg.Warn(Lang.L("Nothing selected") + "\n\n" + Lang.L("Tick what you want to clean first.")); return; }
            StringBuilder names = new StringBuilder();
            for (int i = 0; i < sel.Count && i < 10; i++) names.Append("- " + sel[i].Name + "  (" + sel[i].Size + ")\n");
            if (sel.Count > 10) names.Append("... and " + (sel.Count - 10) + " more\n");
            string extra = "";
            foreach (CleanItem x in sel)
            {
                if (x.Id == "winold") extra += "\n\nWindows.old will be deleted: you will not be able to go back to the previous Windows version.";
                if (x.Id == "recycle") extra += "\n\nThe Recycle Bin will be emptied permanently.";
            }
            if (!Ui.Confirm("Clean " + sel.Count + " item(s)?\n\n" + names.ToString() + extra)) return;

            List<string> ids = new List<string>();
            foreach (CleanItem x in sel) ids.Add(x.Id);
            _resultBox.Visibility = Visibility.Collapsed;

            Work.Run("Cleaning...", delegate(WorkCtx c)
            {
                List<ItemResult> results = new List<ItemResult>();
                int n = ids.Count, i = 0;
                foreach (Def d in Defs)
                {
                    if (!ids.Contains(d.Id)) continue;
                    c.ThrowIfCancelled();
                    double basePct = 100.0 * i / n;
                    c.Progress(basePct, "Cleaning: " + d.Name);
                    ItemResult r = CleanOne(c, d, basePct, 100.0 / n);
                    results.Add(r);
                    i++;
                    c.Progress(100.0 * i / n, "Cleaned: " + d.Name);
                    string f = d.Name + ": " + Fmt.Bytes(r.Before) + " -> " + Fmt.Bytes(r.After) + " (freed " + Fmt.Bytes(r.Freed) + ")";
                    if (r.After > 0)
                    {
                        string why = Reasons(r.InUse, r.Denied, r.Other);
                        c.Warn(f + ", " + Fmt.Bytes(r.After) + " could not be removed" + (why.Length > 0 ? " (" + why + ")" : "") + ".", "Cleanup");
                    }
                    else c.Info(f, "Cleanup");
                    _last[d.Id] = r.Before == 0 ? "already empty"
                        : "last clean: freed " + Fmt.Bytes(r.Freed) + (r.After > 0 ? ", " + Fmt.Bytes(r.After) + " left" : "");
                }

                long before = 0, freed = 0, left = 0; int inUse = 0, denied = 0, other = 0;
                foreach (ItemResult r in results)
                {
                    before += r.Before; freed += r.Freed; left += r.After;
                    inUse += r.InUse; denied += r.Denied; other += r.Other;
                }

                // level: 0 = all good, 1 = partly, 2 = nothing freed
                int level; string text;
                if (before == 0) { level = 0; text = Lang.L("Nothing to clean - these locations are already empty."); }
                else if (left < 1048576 && inUse + denied + other == 0) { level = 0; text = Lang.L("Freed") + " " + Fmt.Bytes(freed) + "."; }
                else
                {
                    string why = Reasons(inUse, denied, other);
                    string tip = inUse > 0 ? " - " + Lang.L("close your browsers and other programs and try again")
                        : (denied > 0 ? " - " + (IsAdmin() ? Lang.L("Windows protects these files") : Lang.L("run Rock Toolbox as administrator")) : "");
                    string lefttxt = Lang.L("Could not remove") + " " + Fmt.Bytes(left) + (why.Length > 0 ? " (" + why + ")" : "") + tip + ".";
                    if (freed < 1024) { level = 2; text = Lang.L("Nothing could be freed.") + " " + lefttxt; }
                    else { level = 1; text = Lang.L("Freed") + " " + Fmt.Bytes(freed) + ". " + lefttxt; }
                }
                if (level == 2) c.Error(text, "Cleanup");
                else c.Info("Cleanup finished: " + text, "Cleanup");

                DoScan(c);   // show the new, real sizes

                int lv = level; string tx = text;
                c.UI(delegate
                {
                    _result.Text = tx;
                    _result.Foreground = Ui.Brush(lv == 0 ? "#30B96B" : (lv == 1 ? "#FFB020" : "#E5484D"));
                    Anim.PopIn(_resultBox, 0.5, 1.0);
                });
                // show the summary after the task has finished (the progress bar is complete by then)
                Ui.Win.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(delegate
                {
                    if (lv == 0) Dlg.Done(Lang.L("Cleanup finished") + "\n\n" + tx);
                    else if (lv == 1) Dlg.Warn(Lang.L("Cleanup incomplete") + "\n\n" + tx);
                    else Dlg.Warn(Lang.L("Nothing was freed") + "\n\n" + tx);
                }));
            });
        }

        // empties a folder that normal deletion cannot touch (wrong owner / ACL): takeown + icacls + "mirror an empty folder"
        private static void ForceEmpty(WorkCtx c, string pattern)
        {
            string dir = pattern.EndsWith("\\*") ? pattern.Substring(0, pattern.Length - 2) : pattern;
            if (!Directory.Exists(dir)) return;
            try
            {
                c.Tool(Tool.System32("takeown.exe"), "/F \"" + dir + "\" /R /D Y");
                c.Tool(Tool.System32("icacls.exe"), "\"" + dir + "\" /grant *S-1-5-32-544:F /T /C /Q");
                string empty = Path.Combine(Path.GetTempPath(), "rk_empty_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(empty);
                c.Tool(Tool.System32("robocopy.exe"), "\"" + empty + "\" \"" + dir + "\" /MIR /R:0 /W:0 /NFL /NDL /NJH /NJS /NP /MT:16");
                try { Directory.Delete(empty, true); } catch (Exception) { }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { }
        }

        private static void DeleteWindowsOld(WorkCtx c, string p)
        {
            if (!Directory.Exists(p)) return;
            c.Tool(Tool.System32("takeown.exe"), "/F \"" + p + "\" /R /D Y");
            c.Tool(Tool.System32("icacls.exe"), "\"" + p + "\" /grant *S-1-5-32-544:F /T /C /Q");
            // multi-threaded robocopy "mirror an empty folder" deletes a huge tree much faster than rd
            string empty = Path.Combine(Path.GetTempPath(), "rk_empty_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(empty);
            c.Tool(Tool.System32("robocopy.exe"), "\"" + empty + "\" \"" + p + "\" /MIR /R:0 /W:0 /NFL /NDL /NJH /NJS /NP /MT:32");
            try { Directory.Delete(empty, true); } catch { }
            c.Tool(Environment.GetEnvironmentVariable("ComSpec"), "/c rd /s /q \"" + p + "\"");
        }
    }
}
