using System.Linq.Expressions;
using WispFramework.RxExtensions.Tests.Structures;
using Xunit;

namespace WispFramework.RxExtensions.Tests.PropertyChain;

public class PropertyChainChangedTests : IDisposable
{
    private readonly Person _person = new()
    {
        Name = "John",
        Address = new Address
        {
            Building = new Building
            {
                ApartmentNumber = 42
            }
        }
    };
    private IDisposable _subscription;
    private int? _lastValue;
    private int _notificationCount;

    public void Dispose()
    {
        _subscription?.Dispose();
    }

    [Fact]
    public void ObservePropertyChain_FullChainValid_ReturnsValue()
    {
        // Arrange
        SetupSubscription(p => p.Address.Building.ApartmentNumber);

        // Assert
        Assert.Equal(42, _lastValue);
        Assert.Equal(1, _notificationCount);
    }

    [Fact]
    public void ObservePropertyChain_NullInMiddleOfChain_ReturnsNull()
    {
        // Arrange
        SetupSubscription(p => p.Address.Building.ApartmentNumber);

        // Act
        _person.Address.Building = null;

        // Assert
        Assert.Null(_lastValue);
        Assert.Equal(2, _notificationCount);
    }

    [Fact]
    public void ObservePropertyChain_RestoringChain_ReturnsValue()
    {
        // Arrange
        SetupSubscription(p => p.Address.Building.ApartmentNumber);
        _person.Address.Building = null;

        // Act
        _person.Address.Building = new Building { ApartmentNumber = 24 };

        // Assert
        Assert.Equal(24, _lastValue);
        Assert.Equal(3, _notificationCount);
    }

    [Fact]
    public void ObservePropertyChain_ChangingValueAtEnd_Notifies()
    {
        // Arrange
        SetupSubscription(p => p.Address.Building.ApartmentNumber);

        // Act
        _person.Address.Building.ApartmentNumber = 99;

        // Assert
        Assert.Equal(99, _lastValue);
        Assert.Equal(2, _notificationCount);
    }

    [Fact]
    public void ObservePropertyChain_NullStartOfChain_ReturnsNull()
    {
        // Arrange
        SetupSubscription(p => p.Address.Building.ApartmentNumber);

        // Act
        _person.Address = null;

        // Assert
        Assert.Null(_lastValue);
        Assert.Equal(2, _notificationCount);
    }

    [Fact]
    public void ObservePropertyChain_MultipleChanges_TracksCorrectly()
    {
        // Arrange
        SetupSubscription(p => p.Address.Building.ApartmentNumber);

        // Act & Assert - Step 1
        _person.Address.Building = null;
        Assert.Null(_lastValue);

        // Act & Assert - Step 2
        _person.Address.Building = new Building { ApartmentNumber = 15 };
        Assert.Equal(15, _lastValue);

        // Act & Assert - Step 3
        _person.Address = null;
        Assert.Null(_lastValue);

        // Act & Assert - Step 4
        _person.Address = new Address { Building = new Building { ApartmentNumber = 77 } };
        Assert.Equal(77, _lastValue);
    }

    [Fact]
    public void ObservePropertyChain_NoChange_DoesNotNotify()
    {
        // Arrange
        SetupSubscription(p => p.Address.Building.ApartmentNumber);

        // Act
        _person.Address.Building.ApartmentNumber = 42; // Same value

        // Assert
        Assert.Equal(42, _lastValue);
        Assert.Equal(1, _notificationCount); // Only initial notification
    }

    private void SetupSubscription(Expression<Func<Person, int>> expression)
    {
        _subscription = _person
            .ObserveValuePropertyChain(expression)
            .Subscribe(value =>
            {
                _lastValue = value;
                _notificationCount++;
            });
    }
}