<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RockToolbox.Native
{
    internal sealed class WuItem : Notify
    {
        private bool _selected;
        public string Title { get; set; }
        public string KB { get; set; }
        public string Size { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public string Id { get; set; }
        public bool Selected { get { return _selected; } set { _selected = value; Raise("Selected"); } }
    }

    // C# port of src\pages\WindowsUpdate.ps1
    internal sealed class WindowsUpdatePage : IPage
    {
        private const string AuKey = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
        private ListView _lv;
        private TextBlock _count, _info, _auto;
        private Button _autoOff, _autoOn;
        private CheckBox _reboot;
        private bool _loaded;

        public string Key { get { return "Windows Update"; } }

        public void Init(Window w, Logger log)
        {
            _lv = Ui.Find<ListView>("lvWU");
            _count = Ui.Find<TextBlock>("txtWUCount");
            _info = Ui.Find<TextBlock>("txtWUInfo");
            _auto = Ui.Find<TextBlock>("txtWUAuto");
            _autoOff = Ui.Find<Button>("btnWUAutoOff");
            _autoOn = Ui.Find<Button>("btnWUAutoOn");
            _reboot = Ui.Find<CheckBox>("chkWUReboot");

            _autoOff.Click += delegate { SetAuto(false); };
            _autoOn.Click += delegate { SetAuto(true); };
            Ui.Find<Button>("btnWUScan").Click += delegate { Scan(); };
            Ui.Find<Button>("btnWUInstall").Click += delegate { InstallSelected(); };
            Ui.Find<Button>("btnWUSelAll").Click += delegate { foreach (WuItem u in Items()) u.Selected = true; UpdateCount(); };
            Ui.Find<Button>("btnWUSelNone").Click += delegate { foreach (WuItem u in Items()) u.Selected = false; UpdateCount(); };
            _lv.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate { UpdateCount(); }));
            _lv.MouseDoubleClick += delegate
            {
                WuItem it = _lv.SelectedItem as WuItem;
                if (it != null) { it.Selected = !it.Selected; UpdateCount(); }
            };
            UpdateAutoText();
        }

        public void OnShown() { UpdateAutoText(); if (!_loaded) Scan(); }

        private List<WuItem> Items() { return (_lv.ItemsSource as List<WuItem>) ?? new List<WuItem>(); }

        private void UpdateCount()
        {
            List<WuItem> all = Items();
            int n = 0;
            foreach (WuItem u in all) if (u.Selected) n++;
            _count.Text = "Selected: " + n + " of " + all.Count;
        }

        private void Scan() { Work.Run("Searching Windows Update...", delegate(WorkCtx c) { DoScan(c); }); }

        private void DoScan(WorkCtx c)
        {
            c.Info("Searching Windows Update (this can take a minute)...", "WinUpdate");
            List<WuRow> rows;
            try { rows = WuApi.Scan(c); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                c.UI(delegate { _info.Text = "Could not reach Windows Update: " + ex.Message + "  (If Windows Update was disabled, turn it back on from the Updates tab.)"; });
                throw;
            }
            List<WuItem> list = new List<WuItem>();
            foreach (WuRow r in rows)
            {
                WuItem o = new WuItem();
                o.Title = r.Title; o.KB = r.KB; o.Size = r.Size; o.Type = r.Type; o.Category = r.Category; o.Id = r.Id;
                list.Add(o);
            }
            c.UI(delegate
            {
                _lv.ItemsSource = list;
                _loaded = true;
                UpdateCount();
                _info.Text = list.Count == 0 ? "Windows is up to date - no pending updates."
                    : list.Count + " update(s) available. Tick the ones you want, then press Install selected.";
            });
            c.Info(list.Count == 0 ? "No Windows updates available." : list.Count + " Windows update(s) available.", "WinUpdate");
        }

        private void InstallSelected()
        {
            List<string> ids = new List<string>();
            foreach (WuItem u in Items()) if (u.Selected) ids.Add(u.Id);
            if (ids.Count == 0) { Ui.Log.Warn("Tick the updates you want to install first.", "WinUpdate"); return; }
            bool restart = _reboot.IsChecked == true;
            Work.Run("Installing updates...", delegate(WorkCtx c)
            {
                WuResult r = WuApi.Install(c, ids);
                c.Info("Finished: " + r.Ok + " installed, " + r.Fail + " failed.", "WinUpdate");
                if (r.Reboot)
                {
                    if (restart)
                    {
                        c.Warn("A restart is needed - restarting in 30 seconds (run \"shutdown /a\" to cancel).", "WinUpdate");
                        try { Tool.Run(Tool.System32("shutdown.exe"), "/r /t 30 /c \"Rock Toolbox: restarting to finish Windows updates\"", null, null); } catch { }
                    }
                    else c.Warn("Restart your PC to finish installing.", "WinUpdate");
                }
                DoScan(c);
            });
        }

        // ---- automatic updates on / off (Windows policy)
        private static bool AutoOn() { return Reg.GetInt(AuKey, "NoAutoUpdate", 0) != 1; }

        private void UpdateAutoText()
        {
            bool on = AutoOn();
            _auto.Text = on ? "Automatic updates: ON" : "Automatic updates: OFF";
            _auto.Foreground = Ui.Brush(on ? "#3DDC84" : "#FF6B6B");
            _autoOff.IsEnabled = on;
            _autoOn.IsEnabled = !on;
        }

        private void SetAuto(bool enable)
        {
            try
            {
                if (enable)
                {
                    Reg.SetDword(AuKey, "NoAutoUpdate", 0);
                    Reg.DeleteValue(AuKey, "AUOptions");
                    Ui.Log.Info("Automatic Windows updates enabled.", "WinUpdate");
                }
                else
                {
                    Reg.SetDword(AuKey, "NoAutoUpdate", 1);
                    Reg.SetDword(AuKey, "AUOptions", 2);
                    Ui.Log.Info("Automatic Windows updates disabled. You can still install updates from this page.", "WinUpdate");
                }
            }
            catch (Exception ex) { Ui.Log.Error("Could not change the setting: " + ex.Message, "WinUpdate"); }
            UpdateAutoText();
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RockToolbox.Native
{
    internal sealed class WuItem : Notify
    {
        private bool _selected;
        public string Title { get; set; }
        public string KB { get; set; }
        public string Size { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public string Id { get; set; }
        public bool Selected { get { return _selected; } set { _selected = value; Raise("Selected"); } }
    }

    // C# port of src\pages\WindowsUpdate.ps1
    internal sealed class WindowsUpdatePage : IPage
    {
        private const string AuKey = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
        private ListView _lv;
        private TextBlock _count, _info, _auto;
        private Button _autoOff, _autoOn;
        private CheckBox _reboot;
        private bool _loaded;

        public string Key { get { return "Windows Update"; } }

        public void Init(Window w, Logger log)
        {
            _lv = Ui.Find<ListView>("lvWU");
            _count = Ui.Find<TextBlock>("txtWUCount");
            _info = Ui.Find<TextBlock>("txtWUInfo");
            _auto = Ui.Find<TextBlock>("txtWUAuto");
            _autoOff = Ui.Find<Button>("btnWUAutoOff");
            _autoOn = Ui.Find<Button>("btnWUAutoOn");
            _reboot = Ui.Find<CheckBox>("chkWUReboot");

            _autoOff.Click += delegate { SetAuto(false); };
            _autoOn.Click += delegate { SetAuto(true); };
            Ui.Find<Button>("btnWUScan").Click += delegate { Scan(); };
            Ui.Find<Button>("btnWUInstall").Click += delegate { InstallSelected(); };
            Ui.Find<Button>("btnWUSelAll").Click += delegate { foreach (WuItem u in Items()) u.Selected = true; UpdateCount(); };
            Ui.Find<Button>("btnWUSelNone").Click += delegate { foreach (WuItem u in Items()) u.Selected = false; UpdateCount(); };
            _lv.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate { UpdateCount(); }));
            _lv.MouseDoubleClick += delegate
            {
                WuItem it = _lv.SelectedItem as WuItem;
                if (it != null) { it.Selected = !it.Selected; UpdateCount(); }
            };
            UpdateAutoText();
        }

        public void OnShown() { UpdateAutoText(); if (!_loaded) Scan(); }

        private List<WuItem> Items() { return (_lv.ItemsSource as List<WuItem>) ?? new List<WuItem>(); }

        private void UpdateCount()
        {
            List<WuItem> all = Items();
            int n = 0;
            foreach (WuItem u in all) if (u.Selected) n++;
            _count.Text = "Selected: " + n + " of " + all.Count;
        }

        private void Scan() { Work.Run("Searching Windows Update...", delegate(WorkCtx c) { DoScan(c); }); }

        private void DoScan(WorkCtx c)
        {
            c.Info("Searching Windows Update (this can take a minute)...", "WinUpdate");
            List<WuRow> rows;
            try { rows = WuApi.Scan(c); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                c.UI(delegate { _info.Text = "Could not reach Windows Update: " + ex.Message + "  (If Windows Update was disabled, turn it back on from the Updates tab.)"; });
                throw;
            }
            List<WuItem> list = new List<WuItem>();
            foreach (WuRow r in rows)
            {
                WuItem o = new WuItem();
                o.Title = r.Title; o.KB = r.KB; o.Size = r.Size; o.Type = r.Type; o.Category = r.Category; o.Id = r.Id;
                list.Add(o);
            }
            c.UI(delegate
            {
                _lv.ItemsSource = list;
                _loaded = true;
                UpdateCount();
                _info.Text = list.Count == 0 ? "Windows is up to date - no pending updates."
                    : list.Count + " update(s) available. Tick the ones you want, then press Install selected.";
            });
            c.Info(list.Count == 0 ? "No Windows updates available." : list.Count + " Windows update(s) available.", "WinUpdate");
        }

        private void InstallSelected()
        {
            List<string> ids = new List<string>();
            foreach (WuItem u in Items()) if (u.Selected) ids.Add(u.Id);
            if (ids.Count == 0) { Ui.Log.Warn("Tick the updates you want to install first.", "WinUpdate"); return; }
            bool restart = _reboot.IsChecked == true;
            Work.Run("Installing updates...", delegate(WorkCtx c)
            {
                WuResult r = WuApi.Install(c, ids);
                c.Info("Finished: " + r.Ok + " installed, " + r.Fail + " failed.", "WinUpdate");
                if (r.Reboot)
                {
                    if (restart)
                    {
                        c.Warn("A restart is needed - restarting in 30 seconds (run \"shutdown /a\" to cancel).", "WinUpdate");
                        try { Tool.Run(Tool.System32("shutdown.exe"), "/r /t 30 /c \"Rock Toolbox: restarting to finish Windows updates\"", null, null); } catch { }
                    }
                    else c.Warn("Restart your PC to finish installing.", "WinUpdate");
                }
                DoScan(c);
            });
        }

        // ---- automatic updates on / off (Windows policy)
        private static bool AutoOn() { return Reg.GetInt(AuKey, "NoAutoUpdate", 0) != 1; }

        private void UpdateAutoText()
        {
            bool on = AutoOn();
            _auto.Text = on ? "Automatic updates: ON" : "Automatic updates: OFF";
            _auto.Foreground = Ui.Brush(on ? "#3DDC84" : "#FF6B6B");
            _autoOff.IsEnabled = on;
            _autoOn.IsEnabled = !on;
        }

        private void SetAuto(bool enable)
        {
            try
            {
                if (enable)
                {
                    Reg.SetDword(AuKey, "NoAutoUpdate", 0);
                    Reg.DeleteValue(AuKey, "AUOptions");
                    Ui.Log.Info("Automatic Windows updates enabled.", "WinUpdate");
                }
                else
                {
                    Reg.SetDword(AuKey, "NoAutoUpdate", 1);
                    Reg.SetDword(AuKey, "AUOptions", 2);
                    Ui.Log.Info("Automatic Windows updates disabled. You can still install updates from this page.", "WinUpdate");
                }
            }
            catch (Exception ex) { Ui.Log.Error("Could not change the setting: " + ex.Message, "WinUpdate"); }
            UpdateAutoText();
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
