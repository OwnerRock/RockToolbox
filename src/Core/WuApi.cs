using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace RockToolbox.Native
{
    internal sealed class WuRow
    {
        public string Title, KB, Size, Type, Category, Id;
    }

    internal sealed class WuResult { public int Ok, Fail; public bool Reboot; }

    // Windows Update Agent through its COM API (Microsoft.Update.Session) - used by the Windows Update and Drivers pages.
    internal static class WuApi
    {
        private static dynamic NewSession()
        {
            return Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session"));
        }

        private static string SizeText(double bytes, double min, double max, string title, string cats)
        {
            double mb = bytes / 1048576.0;
            string size = "-";
            if (mb >= 1024) size = (mb / 1024).ToString("0.0") + " GB";
            else if (mb >= 1) size = mb.ToString("0") + " MB";
            else if (mb > 0) size = "< 1 MB";
            if (min > 0 && max > min * 3 && size != "-") size = "~" + size;
            if (Regex.IsMatch(title, "Security Intelligence Update|Definition Update|Antivirus.*(definition|intelligence)") ||
                Regex.IsMatch(cats, "Definition Updates")) size = "<= 200 MB";
            else if (bytes > 15.0 * 1073741824.0) size = "varies";
            return size;
        }

        // All pending updates (several searches: default / managed / optional / Microsoft Update) as COM objects, de-duplicated.
        private static List<dynamic> FindUpdates(dynamic session, WorkCtx c, string extraCriteria)
        {
            string baseCrit = "IsInstalled=0 and IsHidden=0" + extraCriteria;
            string optCrit = baseCrit + " and DeploymentAction='OptionalInstallation'";
            object[][] sets = new object[][]
            {
                new object[] { 0, baseCrit, "" },
                new object[] { 2, baseCrit, "" },
                new object[] { 2, optCrit, "" },
                new object[] { 0, optCrit, "" },
                new object[] { 3, baseCrit, "7971f918-a847-4430-9279-4a52d1efe18d" }
            };
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            List<dynamic> list = new List<dynamic>();
            int okCount = 0, n = 0;
            string lastErr = "";
            foreach (object[] s in sets)
            {
                n++;
                c.ThrowIfCancelled();
                c.Progress(-1, "Searching Windows Update (" + n + "/" + sets.Length + ")...");
                try
                {
                    dynamic searcher = session.CreateUpdateSearcher();
                    try { searcher.Online = true; } catch { }
                    if ((int)s[0] != 0) searcher.ServerSelection = (int)s[0];
                    if (((string)s[2]).Length > 0) searcher.ServiceID = (string)s[2];
                    dynamic found = searcher.Search((string)s[1]);
                    okCount++;
                    foreach (dynamic u in found.Updates)
                    {
                        string id = Convert.ToString(u.Identity.UpdateID);
                        if (seen.ContainsKey(id)) continue;
                        seen[id] = true;
                        list.Add(u);
                    }
                }
                catch (Exception ex) { lastErr = ex.Message; }
            }
            if (okCount == 0) throw new InvalidOperationException(lastErr);
            return list;
        }

        public static List<WuRow> Scan(WorkCtx c)
        {
            dynamic session = NewSession();
            List<dynamic> all = FindUpdates(session, c, "");
            List<WuRow> rows = new List<WuRow>();
            foreach (dynamic u in all)
            {
                WuRow r = new WuRow();
                r.Title = Convert.ToString(u.Title);
                r.KB = "";
                try { if ((int)u.KBArticleIDs.Count > 0) r.KB = "KB" + Convert.ToString(u.KBArticleIDs.Item(0)); } catch { }
                string cats = "";
                try
                {
                    List<string> names = new List<string>();
                    foreach (dynamic cat in u.Categories) names.Add(Convert.ToString(cat.Name));
                    cats = string.Join(", ", names.ToArray());
                }
                catch { }
                double min = 0, max = 0;
                try { min = Convert.ToDouble(u.MinDownloadSize); } catch { }
                try { max = Convert.ToDouble(u.MaxDownloadSize); } catch { }
                double bytes = min > 0 ? min : max;
                r.Size = SizeText(bytes, min, max, r.Title, cats);
                r.Type = "Software";
                int type = 1;
                try { type = (int)u.Type; } catch { }
                if (type == 2) r.Type = "Driver";
                else if (Regex.IsMatch(cats, "Upgrades") || Regex.IsMatch(r.Title, "^Feature update")) r.Type = "Feature";
                else
                {
                    bool opt = false;
                    try { opt = (bool)u.BrowseOnly; } catch { }
                    if (opt) r.Type = "Optional";
                }
                r.Category = cats;
                r.Id = Convert.ToString(u.Identity.UpdateID);
                rows.Add(r);
            }
            rows.Sort(delegate(WuRow a, WuRow b)
            {
                int x = string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase);
                return x != 0 ? x : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
            });
            return rows;
        }

        public static List<WuRow> ScanDrivers(WorkCtx c)
        {
            dynamic session = NewSession();
            dynamic searcher = session.CreateUpdateSearcher();
            dynamic found = searcher.Search("IsInstalled=0 and Type='Driver' and IsHidden=0");
            List<WuRow> rows = new List<WuRow>();
            foreach (dynamic u in found.Updates)
            {
                WuRow r = new WuRow();
                r.Title = Convert.ToString(u.Title);
                string cls = "", prov = "", date = "";
                try { cls = Convert.ToString(u.DriverClass); } catch { }
                try { prov = Convert.ToString(u.DriverProvider); } catch { }
                try { string mf = Convert.ToString(u.DriverManufacturer); if (!string.IsNullOrEmpty(mf)) prov = mf; } catch { }
                try { date = ((DateTime)u.DriverVerDate).ToString("yyyy-MM-dd"); } catch { }
                double mb = 0;
                try { mb = Convert.ToDouble(u.MaxDownloadSize) / 1048576.0; } catch { }
                string size = "-";
                if (mb >= 1024) size = (mb / 1024).ToString("0.0") + " GB";
                else if (mb >= 1) size = mb.ToString("0") + " MB";
                else if (mb > 0) size = "< 1 MB";
                r.Size = size; r.Type = cls; r.Category = prov; r.KB = date;
                r.Id = Convert.ToString(u.Identity.UpdateID);
                rows.Add(r);
            }
            return rows;
        }

        public static WuResult Install(WorkCtx c, ICollection<string> ids)
        {
            WuResult res = new WuResult();
            dynamic session = NewSession();
            c.Progress(-1, "Preparing the selected updates...");
            List<dynamic> all = FindUpdates(session, c, "");
            List<dynamic> todo = new List<dynamic>();
            foreach (dynamic u in all) if (ids.Contains(Convert.ToString(u.Identity.UpdateID))) todo.Add(u);
            int total = todo.Count;
            if (total == 0) { c.Info("None of the selected updates are available any more.", "WinUpdate"); return res; }
            double span = 100.0 / total;
            int i = 0;
            foreach (dynamic u in todo)
            {
                if (c.Cancelled) { c.Warn("Stopped - the remaining updates were skipped.", "WinUpdate"); break; }
                i++;
                string title = Convert.ToString(u.Title);
                string shortT = title.Length > 46 ? title.Substring(0, 43) + "..." : title;
                double bas = (i - 1) * span;
                c.Progress(bas, null);
                try
                {
                    if (!(bool)u.EulaAccepted) u.AcceptEula();
                    if (!(bool)u.IsDownloaded)
                    {
                        c.Info("Downloading (" + i + "/" + total + "): " + title, "WinUpdate");
                        dynamic coll = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl"));
                        coll.Add(u);
                        dynamic d = session.CreateUpdateDownloader();
                        d.Updates = coll;
                        dynamic job = null;
                        try { job = d.BeginDownload(null, null, null); } catch { job = null; }
                        dynamic dr;
                        if (job != null)
                        {
                            double lastB = 0, speed = 0;
                            DateTime lastT = DateTime.Now;
                            while (!(bool)job.IsCompleted)
                            {
                                if (c.Cancelled) { try { job.RequestAbort(); } catch { } break; }
                                dynamic pr = job.GetProgress();
                                double got = Convert.ToDouble(pr.CurrentUpdateBytesDownloaded);
                                double tot = Convert.ToDouble(pr.CurrentUpdateBytesToDownload);
                                double pc = Convert.ToDouble(pr.PercentComplete);
                                double dt = (DateTime.Now - lastT).TotalSeconds;
                                if (dt >= 1.0) { speed = ((got - lastB) / 1048576.0) / dt; lastB = got; lastT = DateTime.Now; }
                                c.Progress(bas + span * 0.7 * (pc / 100.0),
                                    "Downloading (" + i + "/" + total + "): " + pc.ToString("0") + "% - " + (got / 1048576.0).ToString("0") + " of " +
                                    (tot / 1048576.0).ToString("0") + " MB - " + speed.ToString("0.0") + " MB/s - " + shortT);
                                Thread.Sleep(400);
                            }
                            dr = d.EndDownload(job);
                        }
                        else dr = d.Download();
                        if (c.Cancelled) { c.Warn("  download cancelled", "WinUpdate"); break; }
                        int code = (int)dr.ResultCode;
                        if (code != 2 && code != 3) throw new InvalidOperationException("download failed (code " + code + ")");
                    }
                    c.Progress(bas + span * 0.7, "Installing (" + i + "/" + total + "): " + shortT);
                    c.Info("Installing (" + i + "/" + total + "): " + title, "WinUpdate");
                    dynamic coll2 = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl"));
                    coll2.Add(u);
                    dynamic inst = session.CreateUpdateInstaller();
                    inst.Updates = coll2;
                    dynamic ij = null;
                    try { ij = inst.BeginInstall(null, null, null); } catch { ij = null; }
                    dynamic ir;
                    if (ij != null)
                    {
                        while (!(bool)ij.IsCompleted)
                        {
                            dynamic pr = ij.GetProgress();
                            double pc = Convert.ToDouble(pr.PercentComplete);
                            c.Progress(bas + span * (0.7 + 0.3 * (pc / 100.0)), "Installing (" + i + "/" + total + "): " + pc.ToString("0") + "% - " + shortT);
                            Thread.Sleep(500);
                        }
                        ir = inst.EndInstall(ij);
                    }
                    else ir = inst.Install();
                    int rc = (int)ir.ResultCode;
                    if (rc == 2 || rc == 3)
                    {
                        res.Ok++;
                        if ((bool)ir.RebootRequired) res.Reboot = true;
                        c.Info("  installed", "WinUpdate");
                    }
                    else
                    {
                        res.Fail++;
                        c.Warn("  failed (result " + rc + ", 0x" + ((int)ir.HResult).ToString("X8") + ")", "WinUpdate");
                    }
                }
                catch (Exception ex) { res.Fail++; c.Warn("  failed: " + ex.Message, "WinUpdate"); }
                c.Progress(i * span, null);
            }
            return res;
        }
    }
}
