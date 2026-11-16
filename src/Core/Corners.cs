using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RockToolbox.Native
{
    // Windows 11 rounded window corners (and a dark title bar when the dark look is on) for every window of the program.
    internal static class Corners
    {
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void Init()
        {
            HideScrollCorner();
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(delegate(object s, RoutedEventArgs e) { Apply(s as Window); }));
        }

        // the small square where the horizontal and the vertical scroll bar meet is drawn in the theme colour by default (a white block): make it invisible
        public static void HideScrollCorner()
        {
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ScrollViewer), FrameworkElement.LoadedEvent, new RoutedEventHandler(delegate(object s, RoutedEventArgs e)
            {
                try
                {
                    System.Windows.Controls.ScrollViewer sv = s as System.Windows.Controls.ScrollViewer;
                    if (sv == null || sv.Template == null) return;
                    System.Windows.Shapes.Rectangle r = sv.Template.FindName("Corner", sv) as System.Windows.Shapes.Rectangle;
                    if (r != null) r.Fill = System.Windows.Media.Brushes.Transparent;
                }
                catch (Exception) { }
            }));
        }

        public static void Apply(Window w)
        {
            try
            {
                if (w == null) return;
                IntPtr h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;
                int round = 2;                       // DWMWCP_ROUND
                DwmSetWindowAttribute(h, 33, ref round, 4);
                int dark = Theme.Light ? 0 : 1;      // DWMWA_USE_IMMERSIVE_DARK_MODE
                DwmSetWindowAttribute(h, 20, ref dark, 4);
            }
            catch (Exception) { }
        }
    }
}
