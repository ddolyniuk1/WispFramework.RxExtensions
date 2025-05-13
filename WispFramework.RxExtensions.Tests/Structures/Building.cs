using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WispFramework.RxExtensions.Tests.Structures;

public class Building : INotifyPropertyChanged
{
    private int _apartmentNumber;
    private string _name;
    private bool _buildingCodesArePre2021;

    public int ApartmentNumber
    {
        get => _apartmentNumber;
        set => SetField(ref _apartmentNumber, value);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public bool BuildingCodesArePre2021
    {
        get => _buildingCodesArePre2021;
        set
        {
            if (value == _buildingCodesArePre2021) return;
            _buildingCodesArePre2021 = value;
            OnPropertyChanged();
        }
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