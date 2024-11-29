using System.ComponentModel;
using System.Diagnostics;
using WispFramework.RxExtensions.Tests.Structures;
using Xunit;
using Xunit.Abstractions;

namespace WispFramework.RxExtensions.Tests.Property;

public class PropertyChangedPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly Person _person;
    private IDisposable _subscription;
    private int _reactiveNotificationCount;
    private int _traditionalNotificationCount;
    private const int IterationCount = 10000;

    public PropertyChangedPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _person = new Person
        {
            Name = "John Doe",
            Address = new Address { Street = "123 Main St" }
        };
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }

    [Fact]
    public void ComparePerformance_DirectProperty()
    {
        // Arrange
        SetupSubscriptions();
        var stopwatch = new Stopwatch();
        var results = new List<(string Method, long Milliseconds)>();

        // Warm up
        for (int i = 0; i < 100; i++)
        {
            _person.Name = $"Test {i}";
        }

        _reactiveNotificationCount = 0;
        _traditionalNotificationCount = 0;

        // Test Reactive
        stopwatch.Restart();
        for (int i = 0; i < IterationCount; i++)
        {
            _person.Name = $"Name {i}";
        }

        stopwatch.Stop();
        results.Add(("Reactive", stopwatch.ElapsedMilliseconds));
        var reactiveCount = _reactiveNotificationCount;

        // Reset
        _subscription?.Dispose();
        _reactiveNotificationCount = 0;
        _traditionalNotificationCount = 0;

        // Test Traditional
        stopwatch.Restart();
        for (int i = 0; i < IterationCount; i++)
        {
            _person.Name = $"Name {i}";
        }

        stopwatch.Stop();
        results.Add(("Traditional", stopwatch.ElapsedMilliseconds));
        var traditionalCount = _traditionalNotificationCount;

        // Output results
        _output.WriteLine($"Performance comparison for {IterationCount} property changes:");
        foreach (var result in results)
        {
            _output.WriteLine($"{result.Method}: {result.Milliseconds}ms");
        }

        _output.WriteLine($"Reactive notifications: {reactiveCount}");
        _output.WriteLine($"Traditional notifications: {traditionalCount}");

        // Verify both methods received same number of notifications
        Assert.Equal(traditionalCount, reactiveCount);
    }

    [Fact]
    public void ComparePerformance_NestedProperty()
    {
        // Arrange
        SetupNestedSubscriptions();
        var stopwatch = new Stopwatch();
        var results = new List<(string Method, long Milliseconds)>();

        // Warm up
        for (int i = 0; i < 100; i++)
        {
            _person.Address.Street = $"Street {i}";
        }

        _reactiveNotificationCount = 0;
        _traditionalNotificationCount = 0;

        // Test Reactive
        stopwatch.Restart();
        for (int i = 0; i < IterationCount; i++)
        {
            _person.Address.Street = $"Street {i}";
        }

        stopwatch.Stop();
        results.Add(("Reactive", stopwatch.ElapsedMilliseconds));
        var reactiveCount = _reactiveNotificationCount;

        // Reset
        _subscription?.Dispose();
        _reactiveNotificationCount = 0;
        _traditionalNotificationCount = 0;

        // Test Traditional
        stopwatch.Restart();
        for (int i = 0; i < IterationCount; i++)
        {
            _person.Address.Street = $"Street {i}";
        }

        stopwatch.Stop();
        results.Add(("Traditional", stopwatch.ElapsedMilliseconds));
        var traditionalCount = _traditionalNotificationCount;

        // Output results
        _output.WriteLine($"Performance comparison for {IterationCount} nested property changes:");
        foreach (var result in results)
        {
            _output.WriteLine($"{result.Method}: {result.Milliseconds}ms");
        }

        _output.WriteLine($"Reactive notifications: {reactiveCount}");
        _output.WriteLine($"Traditional notifications: {traditionalCount}");

        // Verify both methods received same number of notifications
        Assert.Equal(traditionalCount, reactiveCount);
    }

    private void SetupSubscriptions()
    {
        // Setup reactive subscription
        _subscription = _person
            .ObserveProperty(p => p.Name)
            .Subscribe(_ => _reactiveNotificationCount++);

        // Setup traditional subscription
        _person.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Person.Name))
            {
                _traditionalNotificationCount++;
            }
        };
    }

    private void SetupNestedSubscriptions()
    {
        // Setup reactive subscription for nested property
        _subscription = _person
            .ObserveProperty(p => p.Address.Street)
            .Subscribe(_ => _reactiveNotificationCount++);

        // Setup traditional subscription for nested property
        void HandlePropertyChanged(object s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Address.Street))
            {
                _traditionalNotificationCount++;
            }
        }

        _person.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Person.Address))
            {
                if (_person.Address != null)
                {
                    _person.Address.PropertyChanged += HandlePropertyChanged;
                }
            }
        };

        if (_person.Address != null)
        {
            _person.Address.PropertyChanged += HandlePropertyChanged;
        }
    }
}