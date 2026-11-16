using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;

namespace RDownloader.Native
{
    // Context handed to a background task: logging, progress, running programs. Replaces PowerShell's Start-Work helpers.
    internal sealed class WorkCtx
    {
        public bool Cancelled { get { return Work.CancelRequested; } }
        public void ThrowIfCancelled() { if (Work.CancelRequested) throw new OperationCanceledException(); }
        public void Log(string msg, string level, string tag) { Ui.Log.Log(msg, level, tag); }
        public void Info(string msg, string tag) { Ui.Log.Log(msg, "INFO", tag); }
        public void Warn(string msg, string tag) { Ui.Log.Log(msg, "WARN", tag); }
        public void Error(string msg, string tag) { Work.Failed++; Ui.Log.Log(msg, "ERROR", tag); }
        public void Progress(double pct, string text) { Work.SetProgress(pct, text); }
        public void UI(Action a) { Ui.Invoke(a); }
        public int Tool(string file, string args, Action<string> onText, Encoding enc) { return RDownloader.Native.Tool.Run(file, args, onText, enc); }
        public int Tool(string file, string args) { return RDownloader.Native.Tool.Run(file, args, null, null); }
    }

    internal static class Work
    {
        public static volatile bool Busy;
        // a stop request is also valid for a task that was abandoned (its generation is no longer the current one)
        private static volatile bool _cancel;
        private static int _gen;
        [ThreadStatic] private static int _myGen;
        public static bool CancelRequested
        {
            get { return _cancel || (_myGen != 0 && _myGen != _gen); }
            set { _cancel = value; }
        }
        public static int Failed;
        private static readonly List<Process> Active = new List<Process>();

        private static ProgressBar _pb;
        private static TextBlock _pct;
        private static TextBlock _status;
        private static Button _stop;

        public static void Init()
        {
            _pb = Ui.Find<ProgressBar>("pbMain");
            _pct = Ui.Find<TextBlock>("txtPct");
            _status = Ui.Find<TextBlock>("txtStatus");
            _stop = Ui.Find<Button>("btnStopGlobal");
            _stop.Click += delegate { RequestCancel(); };
        }

        public static void Register(Process p) { lock (Active) Active.Add(p); }
        public static void Unregister(Process p) { lock (Active) Active.Remove(p); }

        // kills a program together with everything it started (installers, msiexec, child consoles...)
        public static void KillTreePublic(Process p) { KillTree(p); }
        private static void KillTree(Process p)
        {
            try
            {
                ProcessStartInfo k = new ProcessStartInfo(Tool.System32("taskkill.exe"), "/PID " + p.Id + " /T /F");
                k.CreateNoWindow = true; k.UseShellExecute = false;
                Process kp = Process.Start(k);
                if (kp != null) { kp.WaitForExit(4000); kp.Dispose(); }
            }
            catch { }
            try { if (!p.HasExited) p.Kill(); } catch { }
        }

        // Stop: acts at once. The programs are killed, the interface returns to "ready" immediately; a task that is slow to
        // notice (waiting for a service, sleeping) is released after a moment and its late result is ignored.
        public static void RequestCancel()
        {
            if (!Busy || _cancel) return;
            _cancel = true;
            int g = _gen;
            Ui.Log.Warn("Stopping...", "Task");
            _stop.Visibility = System.Windows.Visibility.Collapsed;
            _status.Text = Lang.L("Stopping...");
            Process[] list;
            lock (Active) list = Active.ToArray();
            foreach (Process p in list) KillTree(p);
            System.Windows.Threading.DispatcherTimer t = new System.Windows.Threading.DispatcherTimer();
            t.Interval = TimeSpan.FromMilliseconds(1200);
            t.Tick += delegate
            {
                t.Stop();
                if (!Busy || g != _gen) return;
                _gen++;                       // the old task is abandoned: its checks now see "cancelled", its Finish is ignored
                _cancel = false;
                lock (Active) { foreach (Process p in Active.ToArray()) KillTree(p); Active.Clear(); }
                FinishCore(null, new OperationCanceledException(), true);
                UiHost.RunPending();
            };
            t.Start();
        }

        // pct < 0 = indeterminate
        public static void SetProgress(double pct, string text)
        {
            Ui.Post(delegate
            {
                if (!string.IsNullOrEmpty(text)) _status.Text = text;
                if (pct < 0) { _pb.BeginAnimation(ProgressBar.ValueProperty, null); _pb.IsIndeterminate = true; _pct.Text = ""; }
                else
                {
                    if (pct > 100) pct = 100;
                    _pb.IsIndeterminate = false;
                    Glide(pct);
                    _pct.Text = pct.ToString("0") + "%";
                }
            });
        }

        // the bar value glides to the new value instead of jumping
        private static void Glide(double to)
        {
            try
            {
                System.Windows.Media.Animation.DoubleAnimation a = new System.Windows.Media.Animation.DoubleAnimation(to, new System.Windows.Duration(TimeSpan.FromMilliseconds(380)));
                System.Windows.Media.Animation.CubicEase ce = new System.Windows.Media.Animation.CubicEase(); ce.EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut;
                a.EasingFunction = ce;
                _pb.BeginAnimation(ProgressBar.ValueProperty, a);
            }
            catch { _pb.Value = to; }
        }

        private static void ResetBar()
        {
            _pb.BeginAnimation(ProgressBar.ValueProperty, null);
            _pb.IsIndeterminate = false; _pb.Value = 0; _pct.Text = "";
        }

        // Call on the UI thread. Runs body on a background thread; the window stays responsive.
        public static bool Run(string title, Action<WorkCtx> body)
        {
            if (Busy) { Ui.Log.Warn(_cancel ? "Stopping the previous task, one moment..." : "Another task is still running, please wait...", "Task"); return false; }
            Busy = true;
            CancelRequested = false;
            Failed = 0;
            Ui.Win.Cursor = Cursors.Wait;
            _stop.Visibility = System.Windows.Visibility.Visible;
            _status.Foreground = Ui.Brush("#C9CDE0");
            SetProgress(-1, title);

            int myGen = ++_gen;
            Thread t = new Thread(delegate()
            {
                _myGen = myGen;
                Exception err = null;
                try { body(new WorkCtx()); }
                catch (Exception ex) { err = ex; }
                Ui.Post(delegate { if (myGen == _gen) Finish(title, err); });
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            return true;
        }

        private static void Finish(string title, Exception err)
        {
            FinishCore(title, err, false);
            UiHost.RunPending();      // a look / language change that was waiting for the task to end
        }

        private static void FinishCore(string title, Exception err, bool forced)
        {
            bool cancelled = forced || err is OperationCanceledException || (_cancel && err != null);
            if (_cancel && err == null) { cancelled = true; err = new OperationCanceledException(); }
            Busy = false;
            CancelRequested = false;
            Ui.Win.Cursor = null;
            _stop.Visibility = System.Windows.Visibility.Collapsed;

            if (err == null)
            {
                if (Failed > 0)
                {
                    SetProgress(100, "Finished with errors - check the log");
                    _status.Foreground = Ui.Brush("#FFB454");
                }
                else SetProgress(100, _status.Text == title ? Lang.L("Done") : null);
            }
            else if (cancelled)
            {
                ResetBar();
                _status.Text = Lang.L("Stopped");
                _status.Foreground = Ui.Brush("#FFB454");
                Ui.Log.Warn("Action stopped by user.", "Task");
            }
            else
            {
                ResetBar();
                Ui.Log.Error(err.Message, "Task");
            }
        }
    }
}
