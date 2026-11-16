using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

// RockToolboxDaily.exe - sets today's Bing picture as the desktop wallpaper. Started by the "Rock Daily Wallpaper" scheduled task
// (no admin rights, no window, no PowerShell).
internal static class Daily
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int SystemParametersInfo(int a, int b, string c, int d);

    private static int Main()
    {
        try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"RockToolbox\Wallpapers");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "bing_daily.jpg");
        for (int i = 0; i < 5; i++)
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "Mozilla/5.0 RockToolbox";
                    string json = wc.DownloadString("https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");
                    dynamic r = new JavaScriptSerializer().DeserializeObject(json);
                    string url = "https://www.bing.com" + (string)r["images"][0]["urlbase"] + "_UHD.jpg";
                    wc.DownloadFile(url, file);
                }
                break;
            }
            catch { Thread.Sleep(20000); }
        }
        if (File.Exists(file)) SystemParametersInfo(20, 0, file, 3);
        return 0;
    }
}
