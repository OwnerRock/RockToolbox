using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;

[assembly: AssemblyTitle("R-Downloader")]
[assembly: AssemblyProduct("R-Downloader")]
[assembly: AssemblyCompany("Rock Tech")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace RDownloader.Native
{
    internal static class Program
    {
        public const string Version = "1.2.0";

        [STAThread]
        private static int Main(string[] args)
        {
            int cmdCode;
            if (AppLife.HandleCommand(args, out cmdCode)) return cmdCode;
            bool background = Array.IndexOf(args, "--background") >= 0;
            bool restarted = Array.IndexOf(args, "--restarted") >= 0;

            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RDownloader");
            try { Directory.CreateDirectory(dataDir); } catch { }

            Application app = new Application();
            Corners.Init();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            System.Collections.Generic.List<string> rlists = AppLife.RlistArgs(args);
            if (!AppLife.EnsureSingleInstance(restarted, rlists)) return 0;   // already running: it was asked to come to the front
            app.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                // a UI error must not close the program: write it to error.log and keep running
                try { File.AppendAllText(Path.Combine(dataDir, "error.log"), DateTime.Now.ToString("s") + "  UI error: " + e.Exception + "\r\n", Encoding.UTF8); }
                catch { }
                e.Handled = true;
            };

            try
            {
                Ui.Root = root;
                Migrate.FromRockToolbox(dataDir);
                AppSettings settings = new AppSettings(dataDir);
                Theme.Build(settings);
                UiHost.Root = root; UiHost.DataDir = dataDir;
                Window window = UiHost.BuildMain(settings, true);
                app.MainWindow = window;
                AppLife.InitTray();
                try { BrowserBridge.Start(); } catch { }
                AppLife.QuitEvent += BrowserBridge.Stop;
                bool torrentArg = rlists.Exists(delegate(string x) { return AppLife.IsTorrentItem(x); });
                if (rlists.Count > 0 && !torrentArg)
                {
                    // double-clicked .Rlist file: only the Rlist window, not the whole program
                    AppLife.StartRlistWatch();
                    window.Dispatcher.BeginInvoke(new Action(delegate { foreach (string rp in rlists) { try { DlWin.OpenRlist(rp); } catch (Exception ex) { Ui.Log.Warn("Could not open the Rlist: " + ex.Message, "Rlist"); } } }));
                }
                else if (rlists.Count > 0)
                {
                    window.Show();
                    window.Dispatcher.BeginInvoke(new Action(delegate { foreach (string rp in rlists) AppLife.OpenItem(rp); }));
                }
                else if (!background) window.Show();      // started by Windows at logon: stay in the tray
                return app.Run();
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(dataDir, "error.log"), ex.ToString(), Encoding.UTF8); } catch { }
                string msg = "R-Downloader failed to start\n\n" + ex.Message + "\n\nDetails: " + Path.Combine(dataDir, "error.log");
                try { Dlg.Show(DlgKind.Error, msg, null, "OK", null); }
                catch { MessageBox.Show(msg, "R-Downloader", MessageBoxButton.OK, MessageBoxImage.Error); }
                return 1;
            }
        }
    }

    // First start only: take the look, language and download settings and the download list over from Rock Toolbox (copy, nothing is deleted there).
    internal static class Migrate
    {
        private static readonly string[] Keys = { "Theme", "UiStyle", "Language", "Accent", "AccentHex", "DlDir", "DlMax", "DlLimit", "DlConn", "AriaPath" };

        public static void FromRockToolbox(string dataDir)
        {
            try
            {
                string flag = Path.Combine(dataDir, "migrated.flag");
                if (File.Exists(flag)) return;
                File.WriteAllText(flag, DateTime.Now.ToString("s"));
                string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RockToolbox");
                if (!Directory.Exists(old)) return;
                string myDl = Path.Combine(dataDir, "downloads.json"), oldDl = Path.Combine(old, "downloads.json");
                if (!File.Exists(myDl) && File.Exists(oldDl)) File.Copy(oldDl, myDl);
                string myRl = Path.Combine(dataDir, "rlists.txt"), oldRl = Path.Combine(old, "rlists.txt");
                if (!File.Exists(myRl) && File.Exists(oldRl)) File.Copy(oldRl, myRl);
                string mySet = Path.Combine(dataDir, "settings.json"), oldSet = Path.Combine(old, "settings.json");
                if (!File.Exists(mySet) && File.Exists(oldSet))
                {
                    System.Web.Script.Serialization.JavaScriptSerializer ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                    System.Collections.Generic.Dictionary<string, object> src = ser.DeserializeObject(File.ReadAllText(oldSet, Encoding.UTF8)) as System.Collections.Generic.Dictionary<string, object>;
                    if (src != null)
                    {
                        System.Collections.Generic.Dictionary<string, object> dst = new System.Collections.Generic.Dictionary<string, object>();
                        foreach (string k in Keys) { object v; if (src.TryGetValue(k, out v) && v != null) dst[k] = v; }
                        File.WriteAllText(mySet, ser.Serialize(dst), new UTF8Encoding(true));
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
