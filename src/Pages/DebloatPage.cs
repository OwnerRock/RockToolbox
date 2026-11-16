using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace RockToolbox.Native
{
    // C# port of src\pages\Debloat.ps1: remove preinstalled Store apps (installed + provisioned) and OneDrive.
    internal sealed class DebloatPage : IPage
    {
        private readonly List<CheckBox> _boxes = new List<CheckBox>();

        public static DebloatPage Instance;
        public string Key { get { return "Debloat"; } }
        public List<string> SelectedNames() { List<string> l = new List<string>(); foreach (CheckBox cb in _boxes) if (cb.IsChecked == true) l.Add(((BloatApp)cb.Tag).Name); return l; }
        public void SetNames(List<string> n) { foreach (CheckBox cb in _boxes) cb.IsChecked = n.Contains(((BloatApp)cb.Tag).Name); }
        public void OnShown() { }

        private static Regex Wild(string pat)
        {
            return new Regex("^" + Regex.Escape(pat).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
        }

        private static FrameworkElement Label(string title, string desc)
        {
            StackPanel sp = new StackPanel();
            TextBlock t1 = new TextBlock(); t1.Text = title; t1.FontWeight = FontWeights.SemiBold; t1.Foreground = Ui.Brush("#F2F4FF");
            sp.Children.Add(t1);
            if (!string.IsNullOrEmpty(desc))
            {
                TextBlock t2 = new TextBlock(); t2.Text = desc; t2.FontSize = 11.5; t2.Foreground = Ui.Brush("#8D93AD"); t2.TextWrapping = TextWrapping.Wrap; t2.MaxWidth = 760;
                sp.Children.Add(t2);
            }
            return sp;
        }

        public void Init(Window w, Logger log)
        {
            Instance = this;
            StackPanel colL = Ui.Find<StackPanel>("debloatColL"), colR = Ui.Find<StackPanel>("debloatColR");
            List<string> left = new List<string> { "Microsoft Apps", "Microsoft Ecosystem", "Utilities & Productivity" };
            foreach (string cat in BloatData.Cats)
            {
                StackPanel col = left.Contains(cat) ? colL : colR;
                TextBlock h = new TextBlock(); h.Text = Lang.L(cat); h.Margin = new Thickness(4, 14, 0, 4);
                try { h.Style = (Style)Ui.Win.TryFindResource("H"); } catch { }
                col.Children.Add(h);
                foreach (BloatApp b in BloatData.All)
                {
                    if (b.Cat != cat) continue;
                    CheckBox cb = new CheckBox(); cb.Content = Label(b.Label, b.Name); cb.Tag = b;
                    col.Children.Add(cb); _boxes.Add(cb);
                }
            }
            Ui.Find<Button>("btnDebRec").Click += delegate { foreach (CheckBox cb in _boxes) cb.IsChecked = ((BloatApp)cb.Tag).Rec; };
            Ui.Find<Button>("btnDebAll").Click += delegate { foreach (CheckBox cb in _boxes) cb.IsChecked = true; };
            Ui.Find<Button>("btnDebClear").Click += delegate { foreach (CheckBox cb in _boxes) cb.IsChecked = false; };
            Ui.Find<Button>("btnDebInst").Click += delegate
            {
                Work.Run("Checking installed apps...", delegate(WorkCtx c)
                {
                    c.Info("Checking which of these apps are installed...", "Debloat");
                    List<string> names = new List<string>();
                    try { foreach (AppxInfo p in Appx.List()) names.Add(p.Name); } catch (Exception ex) { c.Warn("Store apps could not be listed: " + ex.Message, "Debloat"); }
                    if (names.Count == 0)
                    {
                        c.Info("Reading the app list with the Windows commands...", "Debloat");
                        try { names = AppxPs.ListNames(c); } catch (OperationCanceledException) { throw; } catch (Exception ex) { c.Warn("Store apps could not be listed: " + ex.Message, "Debloat"); }
                    }
                    int n = 0;
                    c.UI(delegate
                    {
                        foreach (CheckBox cb in _boxes)
                        {
                            BloatApp b = (BloatApp)cb.Tag; bool hit;
                            if (b.Name == "@OneDrive") hit = Removers.OneDriveInstalled();
                            else { Regex rx = Wild(b.Name); hit = names.Exists(delegate(string x) { return rx.IsMatch(x); }); }
                            cb.IsChecked = hit; if (hit) n++;
                        }
                    });
                    c.Info("Selected " + n + " installed app(s).", "Debloat");
                });
            };
            Ui.Find<Button>("btnDebloat").Click += delegate { Remove(); };
        }

        private void Remove()
        {
            List<BloatApp> sel = new List<BloatApp>();
            foreach (CheckBox cb in _boxes) if (cb.IsChecked == true) sel.Add((BloatApp)cb.Tag);
            if (sel.Count == 0) { Ui.Log.Warn("Nothing selected.", "Debloat"); return; }
            Work.Run("Removing apps...", delegate(WorkCtx c)
            {
                if (!Safety.NewRestorePoint(c, "removing apps")) throw new OperationCanceledException();
                c.Info("Reading installed packages...", "Debloat");
                List<AppxInfo> pkgs = new List<AppxInfo>();
                try { pkgs = Appx.List(); } catch (Exception ex) { c.Warn("Store apps could not be listed: " + ex.Message, "Debloat"); }
                int n = sel.Count, i = 0;
                bool nativeOk = pkgs.Count > 0;
                List<BloatApp> viaNative = new List<BloatApp>();
                foreach (BloatApp b in sel)
                {
                    c.ThrowIfCancelled();
                    c.Progress(100.0 * i / n, "Removing: " + b.Label + "  (" + (i + 1) + "/" + n + ")");
                    i++;
                    c.Info("Removing: " + b.Label, "Debloat");
                    if (b.Name == "@OneDrive")
                    {
                        try { Removers.RemoveOneDrive(c); } catch (OperationCanceledException) { throw; } catch (Exception ex) { c.Error("  failed: " + ex.Message, "Debloat"); }
                        continue;
                    }
                    if (!nativeOk) continue;          // the direct way sees no packages on this PC: everything goes through the fallback below
                    viaNative.Add(b);
                    try
                    {
                        Regex rx = Wild(b.Name);
                        int found = 0;
                        foreach (AppxInfo p in pkgs)
                        {
                            if (!rx.IsMatch(p.Name)) continue;
                            found++;
                            try { Appx.Remove(p.FullName); } catch (UnauthorizedAccessException) { /* the Windows commands below remove it */ } catch (Exception ex) { c.Warn("  " + p.Name + ": " + ex.Message, "Debloat"); }
                            // also remove the provisioned copy so it does not return for new users
                            c.Tool(Tool.System32("dism.exe"), "/Online /Remove-ProvisionedAppxPackage /PackageName:" + p.FullName + " /NoRestart /Quiet");
                        }
                        c.Info(found == 0 ? "  (not installed)" : "  removed", "Debloat");
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { c.Error("  failed: " + ex.Message, "Debloat"); }
                }
                // second pass: whatever is still installed (or everything, when the direct way could not list packages) is removed with the Windows commands
                List<BloatApp> check = new List<BloatApp>();
                foreach (BloatApp b in sel) if (b.Name != "@OneDrive") check.Add(b);
                List<string> again = new List<string>(); Dictionary<string, string> labels = new Dictionary<string, string>();
                List<AppxInfo> now = new List<AppxInfo>();
                bool listed = false;
                if (nativeOk) { try { now = Appx.List(); listed = now.Count > 0; } catch (Exception) { } }
                foreach (BloatApp b in check)
                {
                    bool still = true;
                    if (listed) { Regex rx = Wild(b.Name); still = now.Exists(delegate(AppxInfo x) { return rx.IsMatch(x.Name); }); }
                    if (still) { again.Add(b.Name); labels[b.Name] = b.Label; }
                }
                if (again.Count > 0)
                {
                    c.Progress(95, "Removing the rest with the Windows commands...");
                    c.Info("Removing " + again.Count + " app(s) with the Windows app commands...", "Debloat");
                    try { AppxPs.RemoveMany(c, again, labels); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { c.Error("  failed: " + ex.Message, "Debloat"); }
                }
                // Copilot: switch it off for real (policy + taskbar button) and then check that it is really gone
                bool wantCop = false; foreach (BloatApp b in sel) if (b.Name == "*Copilot*") wantCop = true;
                if (wantCop)
                {
                    try
                    {
                        Reg.SetDword(@"HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
                        Reg.SetDword(@"HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
                        Reg.SetDword(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0);
                    }
                    catch (Exception ex) { c.Warn("  Copilot policy: " + ex.Message, "Debloat"); }
                    List<string> left = new List<string>();
                    try { foreach (string nm in AppxPs.ListNames(c)) if (nm.IndexOf("Copilot", StringComparison.OrdinalIgnoreCase) >= 0) left.Add(nm); }
                    catch (OperationCanceledException) { throw; } catch (Exception) { }
                    if (left.Count == 0) c.Info("  Copilot: removed (checked - no Copilot package is installed any more). The taskbar button is hidden.", "Debloat");
                    else c.Warn("  Copilot is still installed: " + string.Join(", ", left.ToArray()) + ". Windows is keeping it - the taskbar button and policy are switched off; sign out or restart and run Debloat again.", "Debloat");
                }
                c.Info("Debloat finished.", "Debloat");
            });
        }
    }
}
