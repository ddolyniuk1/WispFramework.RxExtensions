using System.Reactive.Subjects;
using Xunit;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WispFramework.RxExtensions.Tests.Reactive
{
    public class TakeUntilTests
    {
        [Fact]
        public void TakeUntil_WhenSourceCompletes_CompletesNormally()
        {
            // Arrange
            var subject = new Subject<int>();
            var completed = false;
            using var cts = new CancellationTokenSource();

            // Act
            subject.TakeUntil(cts.Token)
                .Subscribe(
                    _ => { },
                    () => completed = true);

            // Assert
            Assert.False(completed);
            subject.OnCompleted();
            Assert.True(completed);
        }

        [Fact]
        public void TakeUntil_WhenCancelled_CompletesSequence()
        {
            // Arrange
            var subject = new Subject<int>();
            var values = new List<int>();
            var completed = false;
            using var cts = new CancellationTokenSource();

            // Act
            subject.TakeUntil(cts.Token)
                .Subscribe(
                    x => values.Add(x),
                    () => completed = true);

            subject.OnNext(1);
            subject.OnNext(2);
            cts.Cancel();
            subject.OnNext(3); // This should not be received

            // Assert
            Assert.True(completed);
            Assert.Equal(2, values.Count);
            Assert.Equal(new[] { 1, 2 }, values);
        }

        [Fact]
        public void TakeUntil_WhenSourceErrors_PropagatesError()
        {
            // Arrange
            var subject = new Subject<int>();
            var error = default(Exception);
            using var cts = new CancellationTokenSource();
            var expectedError = new Exception("Test error");

            // Act
            subject.TakeUntil(cts.Token)
                .Subscribe(
                    _ => { },
                    ex => error = ex);

            subject.OnError(expectedError);

            // Assert
            Assert.Same(expectedError, error);
        }

        [Fact]
        public void TakeUntil_DisposesRegistrationWhenCompleted()
        {
            // Arrange
            var subject = new Subject<int>();
            var isDisposed = false;
            using var cts = new CancellationTokenSource();

            // Create a disposable that will set our flag
            var testDisposable = Disposable.Create(() => isDisposed = true);

            // Act
            var subscription = subject.TakeUntil(cts.Token)
                .Finally(() => testDisposable.Dispose())
                .Subscribe();

            subscription.Dispose();

            // Assert
            Assert.True(isDisposed);
        }
        [Fact]
        public void TakeUntil_WithNullSource_ThrowsArgumentNullException()
        {
            // Arrange
            IObservable<int> source = null;
            using var cts = new CancellationTokenSource();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => source.TakeUntil(cts.Token));
        }

        [Fact]
        public void TakeUntil_WhenCancelledBeforeSubscription_CompletesImmediately()
        {
            // Arrange
            var subject = new Subject<int>();
            var completed = false;
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel before subscription

            // Act
            subject.TakeUntil(cts.Token)
                .Subscribe(
                    _ => Assert.Fail("Should not receive any values"),
                    () => completed = true);

            subject.OnNext(1); // Should not be received

            // Assert
            Assert.True(completed);
        }

        [Fact]
        public void TakeUntil_MultipleCancellations_CompletesOnlyOnce()
        {
            // Arrange
            var subject = new Subject<int>();
            var completionCount = 0;
            using var cts = new CancellationTokenSource();

            // Act
            subject.TakeUntil(cts.Token)
                .Subscribe(
                    _ => { },
                    () => completionCount++);

            cts.Cancel();
            cts.Cancel(); // Second cancellation should have no effect

            // Assert
            Assert.Equal(1, completionCount);
        }
    }
}