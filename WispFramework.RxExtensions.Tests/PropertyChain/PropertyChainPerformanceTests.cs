using System.ComponentModel;
using System.Diagnostics;
using WispFramework.RxExtensions.Tests.Structures;
using Xunit;
using Xunit.Abstractions;

namespace WispFramework.RxExtensions.Tests.PropertyChain
{
    public class PropertyChainPerformanceTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Person _person;
        private IDisposable _subscription;
        private int? _reactiveValue;
        private int? _traditionalValue;
        private int _reactiveNotificationCount;
        private int _traditionalNotificationCount;
        private const int IterationCount = 10000;

        public PropertyChainPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _person = new Person
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
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        [Fact]
        public void ComparePerformance_PropertyChainVsTraditional()
        {
            // Setup initial state
            _output.WriteLine($"Starting performance test with {IterationCount} iterations\n");

            // Test simple value changes
            _output.WriteLine("Testing simple value changes:");
            var reactiveSimple = RunScenario("Reactive", TestScenario.SimpleValueChanges);
            var traditionalSimple = RunScenario("Traditional", TestScenario.SimpleValueChanges);
            OutputComparisonResults("Simple Value Changes", reactiveSimple, traditionalSimple);

            // Test with null chain elements
            _output.WriteLine("\nTesting with null chain elements:");
            var reactiveNull = RunScenario("Reactive", TestScenario.NullChainElements);
            var traditionalNull = RunScenario("Traditional", TestScenario.NullChainElements);
            OutputComparisonResults("Null Chain Elements", reactiveNull, traditionalNull);

            // Test mixed operations
            _output.WriteLine("\nTesting mixed operations:");
            var reactiveMixed = RunScenario("Reactive", TestScenario.MixedOperations);
            var traditionalMixed = RunScenario("Traditional", TestScenario.MixedOperations);
            OutputComparisonResults("Mixed Operations", reactiveMixed, traditionalNull);
        }

        private enum TestScenario
        {
            SimpleValueChanges,
            NullChainElements,
            MixedOperations
        }

        private (long Duration, int Notifications) RunScenario(string type, TestScenario scenario)
        {
            // Setup subscriptions based on type
            if (type == "Reactive")
            {
                SetupReactiveSubscription();
            }
            else
            {
                SetupTraditionalSubscription();
            }

            // Reset counters
            _reactiveNotificationCount = 0;
            _traditionalNotificationCount = 0;

            // Warm up
            RunIterations(scenario, 100);

            // Reset counters after warm up
            _reactiveNotificationCount = 0;
            _traditionalNotificationCount = 0;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            RunIterations(scenario, IterationCount);

            stopwatch.Stop();

            var count = type == "Reactive" ? _reactiveNotificationCount : _traditionalNotificationCount;

            if (type == "Reactive")
            {
                _subscription?.Dispose();
            }

            return (stopwatch.ElapsedMilliseconds, count);
        }

        private void RunIterations(TestScenario scenario, int iterations)
        {
            switch (scenario)
            {
                case TestScenario.SimpleValueChanges:
                    for (int i = 0; i < iterations; i++)
                    {
                        _person.Address.Building.ApartmentNumber = i;
                    }
                    break;

                case TestScenario.NullChainElements:
                    for (int i = 0; i < iterations; i++)
                    {
                        if (i % 3 == 0)
                        {
                            var oldBuilding = _person.Address.Building;
                            _person.Address.Building = null;
                            _person.Address.Building = oldBuilding;
                        }
                        else
                        {
                            _person.Address.Building.ApartmentNumber = i;
                        }
                    }
                    break;

                case TestScenario.MixedOperations:
                    for (int i = 0; i < iterations; i++)
                    {
                        switch (i % 4)
                        {
                            case 0:
                                _person.Address.Building.ApartmentNumber = i;
                                break;
                            case 1:
                                var oldAddress = _person.Address;
                                _person.Address = null;
                                _person.Address = oldAddress;
                                break;
                            case 2:
                                var oldBuilding = _person.Address.Building;
                                _person.Address.Building = null;
                                _person.Address.Building = oldBuilding;
                                break;
                            case 3:
                                _person.Address.Building.ApartmentNumber = i;
                                break;
                        }
                    }
                    break;
            }
        }

        private void SetupReactiveSubscription()
        {
            _subscription = _person
                .ObserveValuePropertyChain(p => p.Address.Building.ApartmentNumber)
                .Subscribe(value =>
                {
                    _reactiveValue = value;
                    _reactiveNotificationCount++;
                });
        }

        private void SetupTraditionalSubscription()
        {
            Building currentBuilding = null;
            Address currentAddress = null;

            void UpdateValue()
            {
                var newValue = _person?.Address?.Building?.ApartmentNumber;
                // Only increment notification count if the value actually changed
                if (!Equals(_traditionalValue, newValue))
                {
                    _traditionalValue = newValue;
                    _traditionalNotificationCount++;
                }
            }

            void HandleBuildingPropertyChanged(object s, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(Building.ApartmentNumber))
                {
                    UpdateValue();
                }
            }

            void HandleAddressPropertyChanged(object s, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(Address.Building))
                {
                    if (currentBuilding != null)
                    {
                        currentBuilding.PropertyChanged -= HandleBuildingPropertyChanged;
                    }

                    currentBuilding = _person?.Address?.Building;
                    if (currentBuilding != null)
                    {
                        currentBuilding.PropertyChanged += HandleBuildingPropertyChanged;
                    }
                    UpdateValue();
                }
            }

            void HandlePersonPropertyChanged(object s, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(Person.Address))
                {
                    if (currentAddress != null)
                    {
                        currentAddress.PropertyChanged -= HandleAddressPropertyChanged;
                        if (currentBuilding != null)
                        {
                            currentBuilding.PropertyChanged -= HandleBuildingPropertyChanged;
                            currentBuilding = null;
                        }
                    }

                    currentAddress = _person?.Address;
                    if (currentAddress != null)
                    {
                        currentAddress.PropertyChanged += HandleAddressPropertyChanged;
                        currentBuilding = currentAddress.Building;
                        if (currentBuilding != null)
                        {
                            currentBuilding.PropertyChanged += HandleBuildingPropertyChanged;
                        }
                    }
                    UpdateValue();
                }
            }

            // Setup initial handlers
            _person.PropertyChanged += HandlePersonPropertyChanged;

            currentAddress = _person.Address;
            if (currentAddress != null)
            {
                currentAddress.PropertyChanged += HandleAddressPropertyChanged;
                currentBuilding = currentAddress.Building;
                if (currentBuilding != null)
                {
                    currentBuilding.PropertyChanged += HandleBuildingPropertyChanged;
                }
            }

            // Get initial value
            UpdateValue();
        }

        private void OutputComparisonResults(string scenarioName,
            (long Duration, int Notifications) reactive,
            (long Duration, int Notifications) traditional)
        {
            _output.WriteLine($"\n{scenarioName}:");
            _output.WriteLine("Reactive Chain:");
            _output.WriteLine($"  Duration: {reactive.Duration}ms");
            _output.WriteLine($"  Notifications: {reactive.Notifications}");
            _output.WriteLine($"  Operations/sec: {IterationCount * 1000.0 / reactive.Duration:N0}");

            _output.WriteLine("\nTraditional:");
            _output.WriteLine($"  Duration: {traditional.Duration}ms");
            _output.WriteLine($"  Notifications: {traditional.Notifications}");
            _output.WriteLine($"  Operations/sec: {IterationCount * 1000.0 / traditional.Duration:N0}");

            _output.WriteLine($"\nRelative Performance: {traditional.Duration / (double)reactive.Duration:N2}x");
        }
    }
}