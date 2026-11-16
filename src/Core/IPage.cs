using System.Windows;

namespace RockToolbox.Native
{
    // One screen of the app. Init wires the controls of the page's XAML panel, OnShown runs each time its tab opens.
    internal interface IPage
    {
        string Key { get; }
        void Init(Window window, Logger log);
        void OnShown();
    }

    // optional: pages that run timers implement this so they can stop when another tab opens
    internal interface IHidable
    {
        void OnHidden();
    }
}
