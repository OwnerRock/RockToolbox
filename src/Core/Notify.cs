using System.ComponentModel;

namespace RockToolbox.Native
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
