using System.Linq.Expressions;
using Xunit;

namespace WispFramework.RxExtensions.Tests
{
    namespace WispFramework.RxExtensions.Tests.PropertyChangedTests
    {
        public class PropertyChangedTests : IDisposable
        {
            private readonly Person _person = new()
            {
                Name = "John Doe",
                Address = new Address
                {
                    Street = "123 Main St"
                }
            };
            private object _lastValue;
            private int _notificationCount;
            private IDisposable _subscription;

            public void Dispose()
            {
                _subscription?.Dispose();
            }

            [Fact]
            public void ObserveProperty_DirectProperty_NotifiesOnChange()
            {
                // Arrange
                SetupSubscription(p => p.Name);

                // Act
                _person.Name = "Jane Doe";

                // Assert
                Assert.Equal("Jane Doe", _lastValue);
                Assert.Equal(2, _notificationCount); // Initial value + change
            }

            [Fact]
            public void ObserveProperty_NestedProperty_NotifiesOnChange()
            {
                // Arrange
                SetupSubscription(p => p.Address.Street);

                // Act
                _person.Address.Street = "456 Oak Ave";

                // Assert
                Assert.Equal("456 Oak Ave", _lastValue);
                Assert.Equal(2, _notificationCount); // Initial value + change
            }

            [Fact]
            public void ObserveProperty_NestedObjectChange_NotifiesOnChange()
            {
                // Arrange
                SetupSubscription(p => p.Address.Street);
                var newAddress = new Address { Street = "789 Pine Rd" };

                // Act
                _person.Address = newAddress;

                // Assert
                Assert.Equal("789 Pine Rd", _lastValue);
                Assert.Equal(2, _notificationCount); // Initial value + change
            }

            [Fact]
            public void ObserveProperty_MultipleChanges_NotifiesForEachChange()
            {
                // Arrange
                SetupSubscription(p => p.Name);

                // Act
                _person.Name = "Jane Doe";
                _person.Name = "Jim Doe";
                _person.Name = "John Smith";

                // Assert
                Assert.Equal("John Smith", _lastValue);
                Assert.Equal(4, _notificationCount); // Initial value + 3 changes
            }

            [Fact]
            public void ObserveProperty_SameValue_DoesNotNotify()
            {
                // Arrange
                SetupSubscription(p => p.Name);

                // Act
                _person.Name = "John Doe"; // Same as initial value

                // Assert
                Assert.Equal("John Doe", _lastValue);
                Assert.Equal(1, _notificationCount); // Only initial value
            }

            [Fact]
            public void ObserveProperty_NullSource_ThrowsArgumentNullException()
            {
                // Arrange
                Person nullPerson = null;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    nullPerson.ObserveProperty(p => p.Name));
            }

            [Fact]
            public void ObserveProperty_NullExpression_ThrowsArgumentNullException()
            {
                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    _person.ObserveProperty<Person, string>(null));
            }

            private void SetupSubscription<TProperty>(Expression<Func<Person, TProperty>> expression)
            {
                _lastValue = default;
                _notificationCount = 0;
                _subscription = _person
                    .ObserveProperty(expression)
                    .Subscribe(value =>
                    {
                        _lastValue = value;
                        _notificationCount++;
                    });
            }
        }
    }
}