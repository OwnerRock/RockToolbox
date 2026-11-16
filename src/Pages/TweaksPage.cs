using System;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace RockToolbox.Native
{
    // C# port of src\pages\Tweaks.ps1: tweak list (apply / undo), customize switches, power plan, DNS, Explorer restart.
    internal sealed class TweaksPage : IPage
    {
        private StackPanel _tweaksPanel, _customPanel;
        private readonly List<CheckBox> _boxes = new List<CheckBox>();
        private bool _built;

        public static TweaksPage Instance;
        public string Key { get { return "Tweaks"; } }

        public void EnsureBuilt() { if (!_built) Build(); }
        public List<string> SelectedNames() { List<string> l = new List<string>(); foreach (CheckBox cb in _boxes) if (cb.IsChecked == true) l.Add(((Tweak)cb.Tag).Name); return l; }
        public void SetNames(List<string> names) { EnsureBuilt(); foreach (CheckBox cb in _boxes) cb.IsChecked = names.Contains(((Tweak)cb.Tag).Name); }

        private IEnumerable<FrameworkElement> CustomControls()
        {
            foreach (UIElement e in _customPanel.Children)
            {
                CheckBox cb = e as CheckBox; if (cb != null && cb.Tag is Toggle) { yield return cb; continue; }
                StackPanel sp = e as StackPanel;
                if (sp != null) foreach (UIElement x in sp.Children) { ComboBox c = x as ComboBox; if (c != null && c.Tag is Toggle) yield return c; }
            }
        }

        public void ExportCustomize(Dictionary<string, object> toggles, Dictionary<string, object> choices)
        {
            EnsureBuilt();
            foreach (FrameworkElement f in CustomControls())
            {
                CheckBox cb = f as CheckBox; ComboBox c = f as ComboBox;
                if (cb != null) toggles[((Toggle)cb.Tag).Name] = cb.IsChecked == true;
                else if (c != null && c.SelectedItem != null) choices[((Toggle)c.Tag).Name] = Convert.ToString(c.SelectedItem);
            }
        }

        public int ApplyCustomize(Dictionary<string, object> toggles, Dictionary<string, object> choices)
        {
            EnsureBuilt(); int done = 0;
            foreach (FrameworkElement f in CustomControls())
            {
                CheckBox cb = f as CheckBox; ComboBox c = f as ComboBox; object v;
                if (cb != null && toggles != null && toggles.TryGetValue(((Toggle)cb.Tag).Name, out v))
                {
                    bool want = Convert.ToBoolean(v);
                    if ((cb.IsChecked == true) != want)
                        try { ToggleData.SetState((Toggle)cb.Tag, want); cb.IsChecked = want; done++; } catch { Ui.Log.Warn("  failed: " + ((Toggle)cb.Tag).Name, "Profile"); }
                }
                else if (c != null && choices != null && choices.TryGetValue(((Toggle)c.Tag).Name, out v) && c.Items.Contains(Convert.ToString(v)) && Convert.ToString(c.SelectedItem) != Convert.ToString(v))
                { c.SelectedItem = Convert.ToString(v); done++; }
            }
            if (done > 0) SysUtil.RefreshShell();
            return done;
        }

        private static readonly string[] DnsNames = { "Cloudflare (1.1.1.1)", "Google (8.8.8.8)", "Quad9 (9.9.9.9)", "AdGuard (blocks ads)", "Automatic (DHCP)" };
        private static readonly string[][] DnsAddr = {
            new string[] { "1.1.1.1", "1.0.0.1" }, new string[] { "8.8.8.8", "8.8.4.4" }, new string[] { "9.9.9.9", "149.112.112.112" },
            new string[] { "94.140.14.14", "94.140.15.15" }, null };

        public void Init(Window w, Logger log)
        {
            Instance = this;
            _tweaksPanel = Ui.Find<StackPanel>("tweaksPanel");
            _customPanel = Ui.Find<StackPanel>("customizePanel");
            Ui.Find<Button>("btnTweakApply").Click += delegate { Run(true); };
            Ui.Find<Button>("btnTweakUndo").Click += delegate { Run(false); };
            Ui.Find<Button>("btnTweakRec").Click += delegate { foreach (CheckBox cb in _boxes) cb.IsChecked = ((Tweak)cb.Tag).Rec; };
            Ui.Find<Button>("btnTweakClear").Click += delegate { foreach (CheckBox cb in _boxes) cb.IsChecked = false; };
            Ui.Find<Button>("btnExplorer").Click += delegate { SysUtil.KillByName("explorer"); Ui.Log.Info("Explorer restarted.", "Customize"); };

            Ui.Find<Button>("btnUltOn").Click += delegate
            {
                Work.Run("Power plan...", delegate(WorkCtx c)
                {
                    string o = Tool.Capture(Tool.System32("powercfg.exe"), "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
                    Match m = Regex.Match(o, "[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
                    if (m.Success) { c.Tool(Tool.System32("powercfg.exe"), "-setactive " + m.Value); c.Info("Ultimate Performance plan enabled.", "Power"); }
                    else c.Warn("Could not create the Ultimate Performance plan on this edition.", "Power");
                });
            };
            Ui.Find<Button>("btnUltOff").Click += delegate
            {
                Work.Run("Power plan...", delegate(WorkCtx c)
                {
                    c.Tool(Tool.System32("powercfg.exe"), "-setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
                    string list = Tool.Capture(Tool.System32("powercfg.exe"), "-list");
                    foreach (string line in list.Split('\n'))
                        if (line.Contains("Ultimate Performance"))
                        {
                            Match m = Regex.Match(line, "[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
                            if (m.Success) c.Tool(Tool.System32("powercfg.exe"), "-delete " + m.Value);
                        }
                    c.Info("Back to Balanced; Ultimate Performance plan removed.", "Power");
                });
            };
            ComboBox dns = Ui.Find<ComboBox>("cmbDns");
            foreach (string n in DnsNames) dns.Items.Add(n);
            dns.SelectedIndex = 0;
            Ui.Find<Button>("btnDns").Click += delegate
            {
                int idx = dns.SelectedIndex;
                if (idx < 0) return;
                string[] addr = DnsAddr[idx]; string name = DnsNames[idx];
                SetDns(addr, name, null);
            };
        }

        // sets the DNS servers of every active network adapter (null = automatic) and flushes the cache
        public static void SetDns(string[] addr, string name, Action after)
        {
            Work.Run("Setting DNS...", delegate(WorkCtx c)
            {
                c.Info("Setting DNS: " + name, "DNS");
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE"))
                using (ManagementObjectCollection col = s.Get())
                    foreach (ManagementObject a in col)
                    {
                        try
                        {
                            ManagementBaseObject inp = a.GetMethodParameters("SetDNSServerSearchOrder");
                            inp["DNSServerSearchOrder"] = addr;   // null = back to automatic
                            a.InvokeMethod("SetDNSServerSearchOrder", inp, null);
                            c.Info("  adapter: " + Convert.ToString(a["Description"]), "DNS");
                        }
                        catch (Exception ex) { c.Warn("  adapter failed: " + ex.Message, "DNS"); }
                    }
                c.Tool(Tool.System32("ipconfig.exe"), "/flushdns");
                c.Info("DNS updated and cache flushed.", "DNS");
                if (after != null) Ui.Post(after);
            });
        }

        public static void UseCloudflareDns(Action after) { SetDns(DnsAddr[0], DnsNames[0], after); }

        // offered when a download / update fails because website names cannot be resolved
        public static void OfferDnsFix(Action after)
        {
            if (!Ui.Confirm("Fix internet name lookup\n\nThis PC cannot translate website names (DNS), so online features cannot connect.\n\nSwitch the DNS servers to Cloudflare (1.1.1.1)? You can switch back any time in Tweaks > DNS > Automatic (DHCP).")) return;
            SetDns(DnsAddr[0], DnsNames[0], after);
        }

        public void OnShown() { if (!_built) Build(); }

        private void Build()
        {
            _built = true;
            List<Tweak> tweaks = TweakData.Build(SysUtil.IsWin11);
            foreach (string grp in new string[] { "Essential", "Advanced" })
            {
                TextBlock h = new TextBlock();
                try { h.Style = (Style)Ui.Win.FindResource("H"); } catch { }
                if (grp == "Essential") h.Text = Lang.L("Essential Tweaks");
                else { h.Text = Lang.L("Advanced Tweaks - CAUTION"); h.Foreground = Ui.Brush("#FFB454"); }
                _tweaksPanel.Children.Add(h);
                foreach (Tweak t in tweaks)
                {
                    if (t.Group != grp) continue;
                    CheckBox cb = new CheckBox(); cb.Content = t.Name; cb.ToolTip = t.Desc; cb.Tag = t;
                    _tweaksPanel.Children.Add(cb); _boxes.Add(cb);
                }
            }
            BuildCustomize();
        }

        private void Run(bool apply)
        {
            List<Tweak> sel = new List<Tweak>();
            foreach (CheckBox cb in _boxes) if (cb.IsChecked == true) sel.Add((Tweak)cb.Tag);
            if (sel.Count == 0) { Ui.Log.Warn("Nothing selected.", "Tweaks"); return; }
            Work.Run(apply ? "Applying tweaks..." : "Undoing tweaks...", delegate(WorkCtx c)
            {
                if (apply)
                {
                    bool hasRp = sel.Exists(delegate(Tweak t) { return t.Name.StartsWith("Restore Point"); });
                    if (!hasRp && !Safety.NewRestorePoint(c, "running tweaks")) throw new OperationCanceledException();
                }
                int n = sel.Count, i = 0;
                foreach (Tweak t in sel)
                {
                    c.ThrowIfCancelled();
                    c.Progress(100.0 * i / n, (apply ? "Applying: " : "Undoing: ") + t.Name + "  (" + (i + 1) + "/" + n + ")");
                    i++;
                    if (!apply && !t.CanUndo) { c.Warn("No undo available for: " + t.Name, "Tweaks"); continue; }
                    c.Info((apply ? "Applying: " : "Undoing: ") + t.Name + "  (" + i + "/" + n + ")", "Tweaks");
                    try
                    {
                        if (apply) t.Apply(c); else t.Undo(c);
                        c.Info("  done", "Tweaks");
                        SysUtil.RefreshShell();
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { c.Error("  failed: " + ex.Message, "Tweaks"); }
                }
                c.Info(apply ? "Tweaks finished (" + n + ")." : "Undo finished.", "Tweaks");
            });
        }

        // ---------------- customize switches ----------------
        private void BuildCustomize()
        {
            string last = "";
            foreach (Toggle t in ToggleData.Build(SysUtil.IsWin11))
            {
                if (t.Group.Length > 0 && t.Group != last)
                {
                    TextBlock gh = new TextBlock(); gh.Text = t.Group;
                    try { gh.Style = (Style)Ui.Win.FindResource("H"); } catch { }
                    _customPanel.Children.Add(gh);
                    last = t.Group;
                }
                if (t.IsDate) { _customPanel.Children.Add(DateRow(t)); continue; }
                if (t.IsChoice) { _customPanel.Children.Add(ChoiceRow(t)); continue; }
                CheckBox cb = new CheckBox();
                try { cb.Style = (Style)Ui.Win.FindResource("Switch"); } catch { }
                cb.Content = t.Name; cb.ToolTip = t.Desc; cb.Tag = t;
                cb.IsChecked = ToggleData.GetState(t);
                cb.Click += delegate(object s, RoutedEventArgs e)
                {
                    CheckBox box = (CheckBox)s; Toggle tg = (Toggle)box.Tag;
                    bool want = box.IsChecked == true;
                    string q = (want ? "Turn on " : "Turn off ") + tg.Name + "?";
                    if (!Ui.Confirm("Registry Change\n\n" + tg.Desc + "\n\n" + q)) { box.IsChecked = !want; return; }
                    box.IsEnabled = false;
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate
                    {
                        int failed = 0; string err = null;
                        try { failed = ToggleData.SetState(tg, want); }
                        catch (Exception ex) { err = ex.Message; }
                        Ui.Post(delegate
                        {
                            box.IsEnabled = true;
                            if (err != null)
                            {
                                Ui.Log.Error("Failed: " + err, "Customize");
                                box.IsChecked = ToggleData.GetState(tg);
                                return;
                            }
                            if (failed > 0) Ui.Log.Warn("  " + failed + " value(s) of '" + tg.Name + "' could not be written (protected key) - the rest were applied.", "Customize");
                            try { SysUtil.RefreshShell(); } catch { }
                            Ui.Log.Info(tg.Name + ": " + (want ? "ON" : "OFF"), "Customize");
                            if (tg.Restart) Ui.Log.Info("  Restart Explorer (button below) to apply.", "Customize");
                        });
                    });
                };
                _customPanel.Children.Add(cb);
            }
        }

        private static FrameworkElement ChoiceRow(Toggle t)
        {
            StackPanel row = new StackPanel(); row.Orientation = Orientation.Horizontal; row.Margin = new Thickness(4, 6, 4, 6);
            TextBlock lbl = new TextBlock(); lbl.Text = t.Name; lbl.VerticalAlignment = VerticalAlignment.Center; lbl.Margin = new Thickness(0, 0, 16, 0);
            lbl.Foreground = Ui.Brush("#E6E8F2");
            ComboBox cmb = new ComboBox(); cmb.Width = 220; cmb.Height = 28; cmb.VerticalContentAlignment = VerticalAlignment.Center; cmb.ToolTip = t.Desc;
            foreach (string n in t.ChoiceNames) cmb.Items.Add(n);
            int cur = Reg.GetInt(t.ChoiceP, t.ChoiceN, t.ChoiceDef);
            int pick = Array.IndexOf(t.ChoiceValues, cur);
            cmb.SelectedIndex = pick >= 0 ? pick : 0;
            cmb.Tag = t;
            cmb.SelectionChanged += delegate(object s, SelectionChangedEventArgs e)
            {
                ComboBox cb = (ComboBox)s; Toggle tg = (Toggle)cb.Tag;
                if (cb.SelectedIndex < 0) return;
                try
                {
                    Reg.SetDword(tg.ChoiceP, tg.ChoiceN, tg.ChoiceValues[cb.SelectedIndex]);
                    SysUtil.RefreshShell();
                    Ui.Log.Info(tg.Name + ": " + tg.ChoiceNames[cb.SelectedIndex], "Customize");
                }
                catch (Exception ex) { Ui.Log.Error("Failed: " + ex.Message, "Customize"); }
            };
            row.Children.Add(lbl); row.Children.Add(cmb);
            return row;
        }

        private static FrameworkElement DateRow(Toggle t)
        {
            StackPanel row = new StackPanel(); row.Margin = new Thickness(4, 6, 4, 6);
            TextBlock lbl = new TextBlock(); lbl.Text = t.Name + "  (dd/MM/yyyy)"; lbl.Margin = new Thickness(0, 0, 0, 4); lbl.ToolTip = t.Desc;
            lbl.Foreground = Ui.Brush("#E6E8F2");
            StackPanel line = new StackPanel(); line.Orientation = Orientation.Horizontal;
            TextBox box = new TextBox(); box.Width = 150; box.Height = 28; box.VerticalContentAlignment = VerticalAlignment.Center; box.ToolTip = t.Desc;
            string cur = Convert.ToString(Reg.Get(@"HKCU:\Control Panel\International", "sShortDate"));
            box.Text = string.IsNullOrEmpty(cur) ? "dd/MM/yyyy" : cur;
            Button apply = new Button(); apply.Content = "Apply"; apply.Padding = new Thickness(14, 4, 14, 4); apply.Margin = new Thickness(8, 0, 0, 0);
            Button reset = new Button(); reset.Content = "Reset"; reset.Padding = new Thickness(14, 4, 14, 4); reset.Margin = new Thickness(4, 0, 0, 0);
            apply.Click += delegate
            {
                string fmt = box.Text.Trim().Replace('m', 'M');
                if (!Regex.IsMatch(fmt, @"^[dMy]+([\s/\.\-,]+[dMy]+)*$") || !fmt.Contains("d") || !fmt.Contains("M") || !fmt.Contains("y"))
                { Ui.Log.Warn("Date format: use the letters d, M, y with a separator, for example dd/MM/yyyy.", "Customize"); return; }
                try
                {
                    Reg.SetString(@"HKCU:\Control Panel\International", "sShortDate", fmt);
                    box.Text = fmt; SysUtil.RefreshShell();
                    Ui.Log.Info("Date format: " + fmt + "  (" + DateTime.Now.ToString(fmt) + ")", "Customize");
                }
                catch (Exception ex) { Ui.Log.Error("Failed: " + ex.Message, "Customize"); }
            };
            reset.Click += delegate
            {
                try
                {
                    string def = new System.Globalization.CultureInfo(System.Globalization.CultureInfo.CurrentCulture.Name, false).DateTimeFormat.ShortDatePattern;
                    Reg.SetString(@"HKCU:\Control Panel\International", "sShortDate", def);
                    box.Text = def; SysUtil.RefreshShell();
                    Ui.Log.Info("Date format reset: " + def, "Customize");
                }
                catch (Exception ex) { Ui.Log.Error("Failed: " + ex.Message, "Customize"); }
            };
            line.Children.Add(box); line.Children.Add(apply); line.Children.Add(reset);
            row.Children.Add(lbl); row.Children.Add(line);
            return row;
        }
    }
}
