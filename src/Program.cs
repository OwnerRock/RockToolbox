using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;

[assembly: AssemblyTitle("Rock Toolbox")]
[assembly: AssemblyProduct("Rock Toolbox")]
[assembly: AssemblyCompany("Rock Tech")]
[assembly: AssemblyVersion("4.1.0.0")]
[assembly: AssemblyFileVersion("4.1.0.0")]

namespace RockToolbox.Native
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            int cmdCode;
            if (AppLife.HandleCommand(args, out cmdCode)) return cmdCode;
            bool background = Array.IndexOf(args, "--background") >= 0;
            bool restarted = Array.IndexOf(args, "--restarted") >= 0;

            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RockToolbox");
            try { Directory.CreateDirectory(dataDir); } catch { }

            Application app = new Application();
            Corners.Init();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (!AppLife.EnsureSingleInstance(restarted)) return 0;   // already running: it was asked to come to the front
            app.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                // a UI error must not close the tool: write it to error.log and keep running
                try { File.AppendAllText(Path.Combine(dataDir, "error.log"), DateTime.Now.ToString("s") + "  UI error: " + e.Exception + "\r\n", Encoding.UTF8); }
                catch { }
                e.Handled = true;
            };

            try
            {
                Ui.Root = root;
                AppSettings settings = new AppSettings(dataDir);
                Theme.Build(settings);
                // first run of this version: the welcome screen comes first (language, look, profile), then the program starts
                if (!background && settings.GetString("WelcomeSeen") != Welcome.Version)
                {
                    Welcome.Run(settings, root, dataDir);
                    Theme.Build(settings);      // the user may have picked another look / light-dark
                }
                UiHost.Root = root; UiHost.DataDir = dataDir;
                Window window = UiHost.BuildMain(settings, true);
                app.MainWindow = window;
                AppLife.InitTray();
                if (!background) window.Show();      // started by Windows at logon: stay in the tray
                return app.Run();
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(dataDir, "error.log"), ex.ToString(), Encoding.UTF8); } catch { }
                string msg = "Rock Toolbox failed to start\n\n" + ex.Message + "\n\nDetails: " + Path.Combine(dataDir, "error.log");
                try { Dlg.Show(DlgKind.Error, msg, null, "OK", null); }
                catch { MessageBox.Show(msg, "Rock Toolbox", MessageBoxButton.OK, MessageBoxImage.Error); }
                return 1;
            }
        }
    }
}
