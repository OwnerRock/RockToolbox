using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RDownloader.Native
{
    // The icon Windows shows for a file: the program's own icon for downloaded .exe files, otherwise the icon of the program that
    // opens this type (WinRAR for .zip when it is installed, the Explorer zip folder when it is not).
    internal static class FileIcons
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint dwAttributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName; }
        [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO fi, uint size, uint flags);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);

        private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Gate = new object();

        public static ImageSource Get(string fileName, string fullPath)
        {
            try
            {
                string ext = Path.GetExtension(fileName ?? "");
                if (string.IsNullOrEmpty(ext)) ext = ".file";
                bool own = (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".msi", StringComparison.OrdinalIgnoreCase) || ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(fullPath) && File.Exists(fullPath);
                string key = own ? "path:" + fullPath : ext;
                lock (Gate) { ImageSource c; if (Cache.TryGetValue(key, out c)) return c; }
                ImageSource img = null;
                if (own)
                {
                    try
                    {
                        using (System.Drawing.Icon ic = System.Drawing.Icon.ExtractAssociatedIcon(fullPath))
                            if (ic != null) img = ToSource(ic.Handle);
                    }
                    catch (Exception) { img = null; }
                }
                if (img == null)
                {
                    SHFILEINFO fi = new SHFILEINFO();
                    // 0x100 icon | 0x10 use the extension only (the file does not have to exist)
                    SHGetFileInfo("x" + ext, 0x80, ref fi, (uint)Marshal.SizeOf(fi), 0x100 | 0x10);
                    if (fi.hIcon != IntPtr.Zero) { img = ToSource(fi.hIcon); DestroyIcon(fi.hIcon); }
                }
                lock (Gate) { Cache[key] = img; }
                return img;
            }
            catch (Exception) { return null; }
        }

        private static ImageSource ToSource(IntPtr h)
        {
            BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(h, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze(); return bs;
        }
    }
}
