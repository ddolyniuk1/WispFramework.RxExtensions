using System.Linq.Expressions;
using System.Reactive.Linq;
using WispFramework.RxExtensions.Tests.Structures;
using Xunit;

namespace WispFramework.RxExtensions.Tests.PropertyChain;

public class StringPropertyTests : IDisposable
{
    private readonly Person _person = new()
    {
        Name = "John",
        Address = new Address
        {
            Street = "Main St",
            Building = new Building { Name = "Tower A" }
        }
    };
    private IDisposable _subscription;
    private string _lastValue;
    private int _notificationCount;

    public void Dispose()
    {
        _subscription?.Dispose();
    }

    [Fact]
    public void ObservePropertyChain_StringProperty_ReturnsValue()
    {
        SetupSubscription(p => p.Address.Building.Name);
        Assert.Equal("Tower A", _lastValue);
        Assert.Equal(1, _notificationCount);
    }

    [Fact]
    public void ObservePropertyChain_NullStringProperty_ReturnsNull()
    {
        SetupSubscription(p => p.Address.Building.Name);
        _person.Address.Building.Name = null;
        Assert.Null(_lastValue);
        Assert.Equal(2, _notificationCount);
    }

    [Fact]
    public void ObservePropertyChain_EmptyStringProperty_ReturnsEmpty()
    {
        SetupSubscription(p => p.Address.Building.Name);
        _person.Address.Building.Name = string.Empty;
        Assert.Equal(string.Empty, _lastValue);
        Assert.Equal(2, _notificationCount);
    }

    [Fact]
    public void ObservePropertyChain_NullThenRestored_TracksCorrectly()
    {
        SetupSubscription(p => p.Address.Building.Name);
        _person.Address.Building.Name = null;
        Assert.Null(_lastValue);

        _person.Address.Building.Name = "New Tower";
        Assert.Equal("New Tower", _lastValue);
        Assert.Equal(3, _notificationCount);
    }

    [Fact]
    public void ObservePropertyChain_TracksCorrectly()
    {
        SetupSubscription(p => p.Address.Building.Name);
        _person.Address.Building.Name = null;
        Assert.Null(_lastValue);
        _person.Address.Building.Name = "Old Tower";
        Assert.Equal("Old Tower", _lastValue);
        _person.Address.Building.Name = "New Tower";
        Assert.Equal("New Tower", _lastValue);
        Assert.Equal(4, _notificationCount);
    } 

    private void SetupSubscription(Expression<Func<Person, string>> expression)
    {
        _subscription = _person
            .ObserveReferencePropertyChain(expression)
            .Subscribe(value =>
            {
                _lastValue = value;
                _notificationCount++;
            });
    }
}