using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WispFramework.RxExtensions.Tests;

public class Address : INotifyPropertyChanged
{
    private Building _building;
    private string _street;

    public string Street
    {
        get => _street;
        set
        {
            if (value == _street) return;
            _street = value;
            OnPropertyChanged();
        }
    }

    public Building Building
    {
        get => _building;
        set => SetField(ref _building, value);
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