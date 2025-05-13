using System.Reactive.Subjects;
using Xunit;

namespace WispFramework.RxExtensions.Tests.Reactive
{
    public class FireOnChangeEndTests
    {
        [Fact]
        public async Task ShouldFireAfterInactivityPeriod()
        {
            // Arrange
            var source = new Subject<int>();
            var changeEndFired = false;
            var detectionWindow = TimeSpan.FromMilliseconds(100);

            // Act
            var testObservable = source.FireOnChangeEnd(
                detectionWindow,
                () => changeEndFired = true
            );

            using var subscription = testObservable.Subscribe();

            // Push a value and wait longer than detection window
            source.OnNext(1);
            await Task.Delay(detectionWindow + TimeSpan.FromMilliseconds(50));

            // Assert
            Assert.True(changeEndFired);
        }

        [Fact]
        public async Task ShouldNotFireDuringContinuousActivity()
        {
            // Arrange
            var source = new Subject<int>();
            var changeEndCount = 0;
            var detectionWindow = TimeSpan.FromMilliseconds(200);

            // Act
            var testObservable = source.FireOnChangeEnd(
                detectionWindow,
                () => changeEndCount++
            );

            using var subscription = testObservable.Subscribe();

            // Push values frequently (less than detection window)
            source.OnNext(1);
            await Task.Delay(50);
            source.OnNext(2);
            await Task.Delay(50);
            source.OnNext(3);
            await Task.Delay(50);

            // Assert
            Assert.Equal(0, changeEndCount);
        }

        [Fact]
        public async Task ShouldFireMultipleTimesAfterSeparateActivityPeriods()
        {
            // Arrange
            var source = new Subject<int>();
            var changeEndCount = 0;
            var detectionWindow = TimeSpan.FromMilliseconds(100);

            // Act
            var testObservable = source.FireOnChangeEnd(
                detectionWindow,
                () => changeEndCount++
            );

            using var subscription = testObservable.Subscribe();

            // First activity period
            source.OnNext(1);
            await Task.Delay(detectionWindow + TimeSpan.FromMilliseconds(50));

            // Second activity period
            source.OnNext(2);
            await Task.Delay(detectionWindow + TimeSpan.FromMilliseconds(50));

            // Assert
            Assert.Equal(2, changeEndCount);
        }

        [Fact]
        public async Task ShouldNotFireIfActivityResumesBeforeDetectionWindow()
        {
            // Arrange
            var source = new Subject<int>();
            var changeEndCount = 0;
            var detectionWindow = TimeSpan.FromMilliseconds(200);

            // Act
            var testObservable = source.FireOnChangeEnd(
                detectionWindow,
                () => changeEndCount++
            );

            using var subscription = testObservable.Subscribe();

            // Push value and resume activity before detection window
            source.OnNext(1);
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            source.OnNext(2);
            await Task.Delay(TimeSpan.FromMilliseconds(150));

            // Assert
            Assert.Equal(0, changeEndCount);
        }

        [Fact]
        public void ShouldHandleNullAction()
        {
            // Arrange
            var source = new Subject<int>();
            var detectionWindow = TimeSpan.FromMilliseconds(100);

            // Act & Assert (should not throw)
            var testObservable = source.FireOnChangeEnd(
                detectionWindow,
                null
            );

            using var subscription = testObservable.Subscribe();
            source.OnNext(1);
        }
    }
}