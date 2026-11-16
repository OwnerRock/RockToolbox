<<<<<<< HEAD
﻿using System.ComponentModel;

namespace RDownloader.Native
{
    // Base class for list items bound to the XAML (checkbox / status changes refresh the row).
    internal abstract class Notify : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise(string name)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }
    }
}
=======
﻿using System.ComponentModel;

namespace RDownloader.Native
{
    // Base class for list items bound to the XAML (checkbox / status changes refresh the row).
    internal abstract class Notify : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise(string name)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
