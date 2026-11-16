using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace RockToolbox.Native
{
    // C# port of src\pages\Updates.ps1 + features\PauseUpdates.ps1: Windows Update profiles, driver updates switch, pause.
    internal sealed class UpdatesPage : IPage
    {
        private const string WU = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
        private const string UX = @"HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
        private static readonly string[] PauseNames = { "PauseUpdatesStartTime", "PauseUpdatesExpiryTime", "PauseFeatureUpdatesStartTime", "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime" };
        private static readonly string[] PauseLabels = { "1 week", "2 weeks", "3 weeks", "4 weeks", "5 weeks (maximum)" };
        private static readonly int[] PauseDays = { 7, 14, 21, 28, 35 };

        private ComboBox _days;
        private TextBlock _state;

        public string Key { get { return "Updates"; } }
        public void OnShown() { UpdateState(); }

        private static void ResetPolicy()
        {
            Reg.DeleteKey(WU);
            SysUtil.SetStartMode("wuauserv", "Manual");
            SysUtil.SetStartMode("UsoSvc", "Automatic");
        }

        private static void SetTasks(bool enable)
        {
            TaskSched.SetFolderEnabled(@"\Microsoft\Windows\UpdateOrchestrator", enable);
            TaskSched.SetFolderEnabled(@"\Microsoft\Windows\WindowsUpdate", enable);
        }

        public void Init(Window w, Logger log)
        {
            Ui.Find<Button>("btnUpdRec").Click += delegate
            {
                Work.Run("Applying update profile...", delegate(WorkCtx c)
                {
                    ResetPolicy();
                    Reg.SetDword(WU, "DeferFeatureUpdates", 1); Reg.SetDword(WU, "DeferFeatureUpdatesPeriodInDays", 365);
                    Reg.SetDword(WU, "DeferQualityUpdates", 1); Reg.SetDword(WU, "DeferQualityUpdatesPeriodInDays", 4);
                    Reg.SetDword(WU, "ExcludeWUDriversInQualityUpdate", 1);
                    Reg.SetDword(WU + @"\AU", "NoAutoUpdate", 0); Reg.SetDword(WU + @"\AU", "AUOptions", 3); Reg.SetDword(WU + @"\AU", "NoAutoRebootWithLoggedOnUsers", 1);
                    SetTasks(true); SysUtil.StartService("wuauserv");
                    c.Info("Recommended update profile applied. Restart Windows.", "Update");
                });
            };
            Ui.Find<Button>("btnUpdDefault").Click += delegate
            {
                Work.Run("Restoring Windows Update...", delegate(WorkCtx c)
                {
                    ResetPolicy(); SetTasks(true); SysUtil.StartService("wuauserv"); SysUtil.StartService("UsoSvc");
                    c.Info("Windows Update restored to default. Restart Windows.", "Update");
                });
            };
            Ui.Find<Button>("btnUpdDisable").Click += delegate
            {
                Work.Run("Disabling Windows Update...", delegate(WorkCtx c)
                {
                    if (!Safety.NewRestorePoint(c, "disabling Windows Update")) throw new OperationCanceledException();
                    ResetPolicy();
                    Reg.SetDword(WU + @"\AU", "NoAutoUpdate", 1); Reg.SetDword(WU + @"\AU", "AUOptions", 1);
                    foreach (string s in new string[] { "wuauserv", "UsoSvc" }) SysUtil.DisableService(s);
                    SetTasks(false);
                    FastFs.DeletePattern(Environment.ExpandEnvironmentVariables(@"%windir%\SoftwareDistribution\Download\*"));
                    c.Warn("Windows Update disabled. Use Restore Defaults to turn it back on.", "Update");
                });
            };
            Ui.Find<Button>("btnDrvOff").Click += delegate { SetDriverUpdates(false); };
            Ui.Find<Button>("btnDrvOn").Click += delegate { SetDriverUpdates(true); };
            Ui.Find<Button>("btnUpdOpen").Click += delegate { Tool.Start("ms-settings:windowsupdate", ""); };

            _days = Ui.Find<ComboBox>("cmbPauseDays");
            _state = Ui.Find<TextBlock>("txtPauseState");
            foreach (string l in PauseLabels) _days.Items.Add(l);
            _days.SelectedIndex = 1;
            Ui.Find<Button>("btnPause").Click += delegate { Pause(); };
            Ui.Find<Button>("btnResume").Click += delegate
            {
                foreach (string n in PauseNames) Reg.DeleteValue(UX, n);
                UpdateState();
                Ui.Log.Info("Windows updates resumed.", "Update");
            };
            UpdateState();
        }

        private static void SetDriverUpdates(bool allow)
        {
            Toggle drv = ToggleData.Build(SysUtil.IsWin11).Find(delegate(Toggle t) { return t.Name.StartsWith("Automatic Driver"); });
            if (drv == null) return;
            try { ToggleData.SetState(drv, allow); Ui.Log.Info("Drivers via Windows Update: " + (allow ? "allowed." : "blocked."), "Update"); }
            catch (Exception ex) { Ui.Log.Error("Could not change the setting: " + ex.Message, "Update"); }
        }

        private void Pause()
        {
            int idx = _days.SelectedIndex; int days = idx >= 0 ? PauseDays[idx] : 14;
            DateTime start = DateTime.UtcNow, end = start.AddDays(days);
            string fmt = "yyyy-MM-dd'T'HH:mm:ss";
            string s = start.ToString(fmt, CultureInfo.InvariantCulture) + "Z", e = end.ToString(fmt, CultureInfo.InvariantCulture) + "Z";
            Reg.SetString(UX, "PauseUpdatesStartTime", s); Reg.SetString(UX, "PauseUpdatesExpiryTime", e);
            Reg.SetString(UX, "PauseFeatureUpdatesStartTime", s); Reg.SetString(UX, "PauseFeatureUpdatesEndTime", e);
            Reg.SetString(UX, "PauseQualityUpdatesStartTime", s); Reg.SetString(UX, "PauseQualityUpdatesEndTime", e);
            UpdateState();
            Ui.Log.Info("Windows updates paused for " + days + " days (until " + end.ToLocalTime().ToString("yyyy-MM-dd") + ").", "Update");
        }

        private void UpdateState()
        {
            string v = Convert.ToString(Reg.Get(UX, "PauseUpdatesExpiryTime"));
            DateTime d;
            if (!string.IsNullOrEmpty(v) && DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out d) && d > DateTime.UtcNow)
                _state.Text = "Paused until " + d.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            else _state.Text = "Updates are not paused";
        }
    }
}
