using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RockToolbox.Native
{
    // Profile card (name + picture), export / import of the user's selections, first-run welcome window.
    internal static class ProfileUi
    {
        private static string DataDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RockToolbox"); } }

        public static string UserName()
        {
            string n = Ui.Settings.GetString("UserName").Trim();
            if (n.Length > 0) return n;
            return Environment.UserName;
        }

        // ---------------- profile card ----------------
        public static void UpdateCard()
        {
            string n = UserName();
            Ui.Find<TextBlock>("txtUserName").Text = n;
            System.Windows.Shapes.Shape av = Ui.Find<System.Windows.Shapes.Shape>("elAvatar");
            FrameworkElement letter = Ui.Find<FrameworkElement>("txtAvatarLetter");
            string ap = Ui.Settings.GetString("Avatar");
            bool shown = false;
            if (ap.Length > 0 && File.Exists(ap))
            {
                try
                {
                    BitmapImage bi = new BitmapImage(); bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bi.UriSource = new Uri(ap); bi.DecodePixelWidth = 128; bi.EndInit(); bi.Freeze();
                    ImageBrush ib = new ImageBrush(bi); ib.Stretch = Stretch.UniformToFill; av.Fill = ib; letter.Visibility = Visibility.Collapsed; shown = true;
                }
                catch { }
            }
            if (!shown)
            {
                av.Fill = Ui.Brush("#B8C0CC"); letter.Visibility = Visibility.Visible;
            }
        }

        public static void EditProfile() { Welcome.ShowProfile(); }

        // ---------------- export / import ----------------
        public static void Export()
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "Rock Toolbox profile (*.json)|*.json"; dlg.FileName = "RockToolbox-profile.json";
            dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (dlg.ShowDialog() != true) return;
            try
            {
                List<string> tw = new List<string>(), db = new List<string>(), apps = new List<string>();
                Dictionary<string, object> toggles = new Dictionary<string, object>(), choices = new Dictionary<string, object>();
                if (TweaksPage.Instance != null) { tw = TweaksPage.Instance.SelectedNames(); TweaksPage.Instance.ExportCustomize(toggles, choices); }
                if (DebloatPage.Instance != null) db = DebloatPage.Instance.SelectedNames();
                if (InstallPage.Instance != null) apps = InstallPage.Instance.SelectedIds();
                Dictionary<string, object> obj = new Dictionary<string, object>();
                obj["Tool"] = "RockToolbox"; obj["Version"] = 1; obj["Created"] = DateTime.Now.ToString("s");
                obj["Tweaks"] = tw; obj["Debloat"] = db; obj["Apps"] = apps;
                Dictionary<string, object> cu = new Dictionary<string, object>(); cu["Toggles"] = toggles; cu["Choices"] = choices; obj["Customize"] = cu;
                File.WriteAllText(dlg.FileName, new JavaScriptSerializer().Serialize(obj), new UTF8Encoding(true));
                Ui.Log.Info("Profile saved: " + dlg.FileName + "  (" + tw.Count + " tweaks, " + db.Count + " debloat, " + apps.Count + " apps)", "Profile");
            }
            catch (Exception ex) { Ui.Log.Error("Could not save the profile: " + ex.Message, "Profile"); }
        }

        private static List<string> Strs(object o)
        {
            List<string> l = new List<string>(); ArrayList a = Net.Arr(o);
            if (a != null) foreach (object x in a) l.Add(Convert.ToString(x));
            return l;
        }

        private static Dictionary<string, object> Dict(object o) { return o as Dictionary<string, object>; }

        public static void Import()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Rock Toolbox profile (*.json)|*.json"; dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (dlg.ShowDialog() != true) return;
            Dictionary<string, object> j = null;
            try { j = Dict(new JavaScriptSerializer().DeserializeObject(File.ReadAllText(dlg.FileName, Encoding.UTF8))); } catch { }
            if (j == null || !j.ContainsKey("Tool") || Convert.ToString(j["Tool"]) != "RockToolbox") { Ui.Log.Warn("That file is not a Rock Toolbox profile.", "Profile"); return; }
            List<string> tw = Strs(j.ContainsKey("Tweaks") ? j["Tweaks"] : null), db = Strs(j.ContainsKey("Debloat") ? j["Debloat"] : null), apps = Strs(j.ContainsKey("Apps") ? j["Apps"] : null);
            if (TweaksPage.Instance != null) TweaksPage.Instance.SetNames(tw);
            if (DebloatPage.Instance != null) DebloatPage.Instance.SetNames(db);
            if (InstallPage.Instance != null) InstallPage.Instance.SetSelected(apps);
            Ui.Log.Info("Profile loaded: " + tw.Count + " tweaks, " + db.Count + " debloat items and " + apps.Count + " apps are now ticked. Review them in each tab, then run.", "Profile");
            Dictionary<string, object> cu = j.ContainsKey("Customize") ? Dict(j["Customize"]) : null;
            if (cu == null || TweaksPage.Instance == null) return;
            Dictionary<string, object> tg = cu.ContainsKey("Toggles") ? Dict(cu["Toggles"]) : null, ch = cu.ContainsKey("Choices") ? Dict(cu["Choices"]) : null;
            int n = (tg == null ? 0 : tg.Count) + (ch == null ? 0 : ch.Count);
            if (n > 0 && Ui.Confirm("The profile also holds " + n + " Customize preferences (Config switches, dropdowns).\n\nApply them now? They change Windows settings."))
            {
                int done = TweaksPage.Instance.ApplyCustomize(tg, ch);
                Ui.Log.Info("Applied " + done + " Customize preference(s).", "Profile");
            }
        }
    }
}
