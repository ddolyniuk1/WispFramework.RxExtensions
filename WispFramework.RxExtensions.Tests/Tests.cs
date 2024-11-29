using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace WispFramework.RxExtensions.Tests
{
    public class ReactiveExtensionsTests
    {
        [Fact]
        public async Task SwitchSelect_ShouldSwitchToLatestSequence()
        {
            var values = new Subject<int>();
            var results = new List<string>();

            values.SwitchSelect(x =>
                Observable.Timer(TimeSpan.FromMilliseconds(x))
                    .Select(_ => $"Value{x}"))
                .Subscribe(results.Add);

            values.OnNext(200);
            values.OnNext(100); // Should cancel 200ms operation

            await Task.Delay(300);

            Assert.Single(results);
            Assert.Equal("Value100", results[0]);
        }

        [Fact]
        public async Task ConcatSelect_ShouldProcessSequentially()
        {
            var values = new Subject<int>();
            var results = new List<string>();

            values.ConcatSelect(x =>
                Observable.Timer(TimeSpan.FromMilliseconds(x))
                    .Select(_ => $"Value{x}"))
                .Subscribe(results.Add);

            values.OnNext(200);
            values.OnNext(100);

            await Task.Delay(300);

            Assert.Equal(2, results.Count);
            Assert.Equal("Value200", results[0]);
            Assert.Equal("Value100", results[1]);
        }

        [Fact]
        public async Task MergeSelect_ShouldProcessConcurrently()
        {
            var values = new Subject<int>();
            var results = new List<string>();

            values.MergeSelect(x =>
                Observable.Timer(TimeSpan.FromMilliseconds(x))
                    .Select(_ => $"Value{x}"))
                .Subscribe(results.Add);

            values.OnNext(200);
            values.OnNext(100);

            await Task.Delay(300);

            Assert.Equal(2, results.Count);
            Assert.Equal("Value100", results[0]);
            Assert.Equal("Value200", results[1]);
        }

        [Fact]
        public async Task ToUnit_ShouldIgnoreOperationResults()
        {
            var values = new Subject<int>();
            var count = 0;

            values.ToUnit(x =>
                Observable.Timer(TimeSpan.FromMilliseconds(x))
                    .Select(_ => $"Value{x}"))
                .Subscribe(_ => count++);

            values.OnNext(100);

            await Task.Delay(200);

            Assert.Equal(1, count);
        }
 

        [Fact]
        public async Task QueueLatestWhileBusy_WithObservable_ShouldRespectShortCircuit()
        {
            var values = new Subject<int>();
            var shortCircuit = new Subject<bool>();
            var results = new List<int>();

            values.QueueLatestWhileBusy(
                    x => Observable.Timer(TimeSpan.FromMilliseconds(1000))
                        .SelectMany(_ => Observable.Start(() => results.Add(x))), i => shortCircuit)
                .Subscribe();

            values.OnNext(1);
            shortCircuit.OnNext(true);
            values.OnNext(2);

            await Task.Delay(3000);

            Assert.Equal(new[] { 2 }, results);
        }
         
        [Fact]
        public async Task RetryWithDelay_ShouldRetryWithSpecifiedDelay()
        {
            var attempts = 0;
            var source = Observable.Create<int>(observer =>
            {
                attempts++;
                if (attempts < 3)
                {
                    observer.OnError(new Exception());
                }
                else
                {
                    observer.OnNext(attempts);
                    observer.OnCompleted();
                }
                return Disposable.Empty;
            });

            var result = await source
                .RetryWithDelay(3, attempt => TimeSpan.FromMilliseconds(100 * attempt))
                .FirstAsync();

            Assert.Equal(3, result);
            Assert.Equal(3, attempts);
        }
    }
}
