using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WispFramework.RxExtensions.Tests.Structures
{
    public class Person : INotifyPropertyChanged
    {
        private string _name;
        private Address _address;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public Address Address
        {
            get => _address;
            set => SetField(ref _address, value);
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}