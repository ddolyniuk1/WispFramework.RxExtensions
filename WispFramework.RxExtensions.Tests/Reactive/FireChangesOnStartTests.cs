using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace WispFramework.RxExtensions.Tests.Reactive
{
    public class FireOnChangeStartTests
    {
        [Fact]
        public async Task ShouldFireAfterDetectionWindow()
        {
            // Arrange
            var source = new Subject<int>();
            var changeStartFired = false;
            var detectionWindow = TimeSpan.FromMilliseconds(100);

            // Act
            var testObservable = source.FireOnChangeStart(
                detectionWindow,
                () => changeStartFired = true
            );

            using var subscription = testObservable.Subscribe();

            // Push a value and wait slightly longer than detection window
            source.OnNext(1);
            await Task.Delay(detectionWindow + TimeSpan.FromMilliseconds(50));

            // Assert
            Assert.True(changeStartFired);
        }

        [Fact]
        public async Task ShouldNotFireBeforeDetectionWindow()
        {
            // Arrange
            var source = new Subject<int>();
            var changeStartFired = false;
            var detectionWindow = TimeSpan.FromMilliseconds(100);

            // Act
            var testObservable = source.FireOnChangeStart(
                detectionWindow,
                () => changeStartFired = true
            );

            using var subscription = testObservable.Subscribe();

            // Push a value and wait less than detection window
            source.OnNext(1);
            await Task.Delay(TimeSpan.FromMilliseconds(50));

            // Assert
            Assert.False(changeStartFired);
        }

        [Fact]
        public async Task ShouldFireOnlyOnce()
        {
            // Arrange
            var source = new Subject<int>();
            var changeStartCount = 0;
            var detectionWindow = TimeSpan.FromMilliseconds(100);

            // Act
            var testObservable = source.FireOnChangeStart(
                detectionWindow,
                () => changeStartCount++
            );

            using var subscription = testObservable.Subscribe();

            // Push multiple values
            source.OnNext(1);
            source.OnNext(2);
            source.OnNext(3);

            await Task.Delay(detectionWindow + TimeSpan.FromMilliseconds(50));

            // Assert
            Assert.Equal(1, changeStartCount);
        }

        [Fact]
        public void ShouldPassThroughAllValues()
        {
            // Arrange
            var source = new Subject<int>();
            var receivedValues = new List<int>();
            var detectionWindow = TimeSpan.FromMilliseconds(100);

            // Act
            var testObservable = source.FireOnChangeStart(
                detectionWindow,
                () => { }
            );

            using var subscription = testObservable.Subscribe(x => receivedValues.Add(x));

            // Push values
            source.OnNext(1);
            source.OnNext(2);
            source.OnNext(3);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, receivedValues);
        }
    }
}
