<<<<<<< HEAD
﻿using System.Windows;

namespace RDownloader.Native
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
=======
﻿using System.Windows;

namespace RDownloader.Native
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
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
