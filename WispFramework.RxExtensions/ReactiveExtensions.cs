using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;

// ReSharper disable UnusedMember.Global

namespace WispFramework.RxExtensions
{
    public static class ReactiveExtensions
    {
        /// <summary>
        /// Projects each element of the source sequence into an observable sequence and flattens using Switch semantics.
        /// Only the latest sequence is processed, unsubscribing from previous sequences.
        /// </summary>
        /// <typeparam name="TInput">The type of elements in the source sequence</typeparam>
        /// <typeparam name="TOutput">The type of elements in the result sequences</typeparam>
        /// <param name="source">The source observable sequence</param>
        /// <param name="selector">Transform function to apply to each element</param>
        /// <returns>An observable sequence whose elements are the result of invoking the transform function 
        /// on each element of source and switching to the latest sequence</returns>
        public static IObservable<TOutput> SwitchSelect<TInput, TOutput>(this IObservable<TInput> source,
            Func<TInput, IObservable<TOutput>> selector) => source
            .Select(selector)
            .Switch();

        /// <summary>
        /// Projects each element of the source sequence into an observable sequence and flattens using Concat semantics.
        /// Sequences are processed sequentially in arrival order.
        /// </summary>
        /// <typeparam name="TInput">The type of elements in the source sequence</typeparam>
        /// <typeparam name="TOutput">The type of elements in the result sequences</typeparam>
        /// <param name="source">The source observable sequence</param>
        /// <param name="selector">Transform function to apply to each element</param>
        /// <returns>An observable sequence whose elements are the result of invoking the transform function 
        /// on each element of source and concatenating the sequences</returns>
        public static IObservable<TOutput> ConcatSelect<TInput, TOutput>(this IObservable<TInput> source,
            Func<TInput, IObservable<TOutput>> selector) => source
            .Select(selector)
            .Concat();

        /// <summary>
        /// Projects each element of the source sequence into an observable sequence and flattens using Merge semantics.
        /// All sequences are processed concurrently.
        /// </summary>
        /// <typeparam name="TInput">The type of elements in the source sequence</typeparam>
        /// <typeparam name="TOutput">The type of elements in the result sequences</typeparam>
        /// <param name="source">The source observable sequence</param>
        /// <param name="selector">Transform function to apply to each element</param>
        /// <returns>An observable sequence whose elements are the result of invoking the transform function 
        /// on each element of source and merging the sequences</returns>
        public static IObservable<TOutput> MergeSelect<TInput, TOutput>(this IObservable<TInput> source,
            Func<TInput, IObservable<TOutput>> selector) => source
            .Select(selector)
            .Merge();

        /// <summary>
        /// Projects each element of the source sequence into an observable sequence and transforms the result into Unit.
        /// Useful for operations where the output values are not needed.
        /// </summary>
        /// <typeparam name="TInput">The type of elements in the source sequence</typeparam>
        /// <typeparam name="TOutput">The type of elements in the intermediate sequences</typeparam>
        /// <param name="source">The source observable sequence</param>
        /// <param name="selector">Transform function to apply to each element</param>
        /// <returns>An observable sequence of Unit values</returns>
        public static IObservable<Unit> ToUnit<TInput, TOutput>(this IObservable<TInput> source) => source
            .Select(_ => Unit.Default);

        public static IObservable<T> FireOnChangeStart<T>(
            this IObservable<T> source,
            TimeSpan detectionWindow,
            Action onChangeStart)
        {
            return Observable.Create<T>(observer =>
            {
                var disposable = new CompositeDisposable();
                var hasStarted = false;
                var changeDetected = new Subject<Unit>();

                changeDetected
                    .DisposeWith(disposable);

                source
                    .Do(_ =>
                    {
                        if (hasStarted) return;
                        hasStarted = true;
                        changeDetected.OnNext(Unit.Default);
                    })
                    .Subscribe(observer)
                    .DisposeWith(disposable);

                changeDetected
                    .Take(1)
                    .Delay(detectionWindow)
                    .Subscribe(_ => onChangeStart())
                    .DisposeWith(disposable);

                return disposable;
            });
        }

        public static IObservable<T> FireOnChangeEnd<T>(
            this IObservable<T> source,
            TimeSpan detectionWindow,
            Action onChangeEnd)
        {
            return Observable.Create<T>(observer =>
            { 
                var disposable = new CompositeDisposable();

                source
                    .Throttle(detectionWindow) 
                    .Subscribe(_ =>
                    {
                        onChangeEnd?.Invoke();
                    })
                    .DisposeWith(disposable);

                return disposable;
            });
        }

        public static IObservable<TResult> ProcessLatestSequentially<TSource, TResult>(
            this IObservable<TSource> source,
            Func<TSource, IObservable<TResult>> workSelector,
            Action<TSource, Exception>? onErrorInWork = null,
            IScheduler? scheduler = null)
        {
            scheduler ??= Scheduler.Default;

            onErrorInWork ??= (_, _) => { };
            var latestHotSource = source.Replay(1).RefCount();

            return Observable.Create<TResult>(observer =>
            {
                var trigger = new Subject<Unit>();
                var disposables = new CompositeDisposable();

                disposables.Add(trigger);

                var processingSubscription = trigger
                    .ObserveOn(scheduler)
                    .ConcatSelect(_ =>
                        latestHotSource
                            .Take(1)
                            .SelectMany(itemToProcess =>
                            {
                                IObservable<TResult> workObservable;
                                try
                                {
                                    workObservable = workSelector(itemToProcess);
                                }
                                catch (Exception ex)
                                {
                                    onErrorInWork(itemToProcess, ex);
                                    scheduler.Schedule(() => trigger.OnNext(Unit.Default));
                                    return Observable.Empty<TResult>();
                                }

                                return workObservable
                                    .Do(
                                        _ => { },
                                        ex => { },
                                        () => { scheduler.Schedule(() => trigger.OnNext(Unit.Default)); }
                                    )
                                    .Catch<TResult, Exception>(ex =>
                                    {
                                        onErrorInWork(itemToProcess, ex);
                                        scheduler.Schedule(() => trigger.OnNext(Unit.Default));
                                        return Observable.Empty<TResult>();
                                    });
                            })
                    )
                    .Subscribe(observer);

                disposables.Add(processingSubscription);

                var sourceTerminationSubscription = latestHotSource
                    .Materialize()
                    .ObserveOn(scheduler)
                    .Subscribe(notification =>
                    {
                        switch (notification.Kind)
                        {
                            case NotificationKind.OnError:
                                observer.OnError(notification?.Exception ?? new UnspecifiedExceptionOccurred());
                                trigger.OnCompleted();
                                break;
                            case NotificationKind.OnCompleted:
                                trigger.OnCompleted();
                                break;
                        }
                    });
                disposables.Add(sourceTerminationSubscription);

                scheduler.Schedule(() => trigger.OnNext(Unit.Default));

                return disposables;
            });
        }

        public static IObservable<T> TakeUntil<T>(
            this IObservable<T> source,
            CancellationToken cancellationToken)
        {  
            if(source == null) throw new ArgumentNullException("source");
            return Observable.Create<T>(observer =>
            {
                var disposable = new CompositeDisposable();
                var serialDisposable = new SerialDisposable();
                serialDisposable.DisposeWith(disposable);
                source.Subscribe(observer)
                    .DisposeWith(serialDisposable);
                CancellationTokenRegistration? registration = null;
                Observable
                    .FromEvent(
                        h =>
                        {
                            registration = cancellationToken.Register(h);
                        },
                        h =>
                        {
                            registration?.Dispose();
                            registration = null;
                        })
                    .Take(1)
                    .Subscribe(_ =>
                    {
                        observer.OnCompleted();
                        serialDisposable?.Disposable?.Dispose();
                    }).DisposeWith(disposable);

                Disposable.Create(() =>
                {
                    registration?.Dispose();
                    registration = null;
                }).DisposeWith(disposable);

                return disposable;
            });
        }
        public static IObservable<T> QueueLatestWhileBusy<T>(
            this IObservable<T> source,
            Func<T, IObservable<Unit>> operation,
            Func<T, IObservable<bool>> shortCircuit)
        {
            return Observable.Create<T>(observer =>
            {
                var hasChanged = false;
                var busy = new BehaviorSubject<bool>(false);
                var latest = new BehaviorSubject<T>(default);
                var disposable = new CompositeDisposable();
                var serialDisposable = new SerialDisposable();
                serialDisposable.DisposeWith(disposable);

                source.Subscribe(value =>
                {
                    latest.OnNext(value);
                    if (!busy.Value)
                    {
                        ProcessValue(value);
                    }
                    else
                    {
                        hasChanged = true;
                    }
                }).DisposeWith(disposable);

                void ProcessValue(T value)
                {
                    busy.OnNext(true);
                    var internalComp = new CompositeDisposable();

                    var testObs = shortCircuit(value);
                    operation(value)
                        .TakeUntil(testObs.Where(t => t))
                        .Subscribe(_ => { busy.OnNext(false); }).DisposeWith(internalComp);

                    testObs.Subscribe(b =>
                        {
                            if (b)
                            {
                                busy.OnNext(false);
                            }
                        })
                        .DisposeWith(internalComp);

                    internalComp.DisposeWith(serialDisposable);
                }

                busy.Subscribe(b =>
                {
                    if (!b && hasChanged)
                    {
                        hasChanged = false;
                        ProcessValue(latest.Value);
                    }
                }).DisposeWith(disposable);

                return disposable;
            });
        }

        /// <summary>
        /// Retries an operation with a dynamic delay between attempts.
        /// </summary>
        /// <typeparam name="T">The type of elements in the sequence</typeparam>
        /// <param name="source">The source observable sequence</param>
        /// <param name="retryCount">Maximum number of retry attempts</param>
        /// <param name="getDelay">Function that returns the delay duration based on the current retry attempt</param>
        /// <returns>An observable sequence that retries on error with specified delays</returns>
        public static IObservable<T> RetryWithDelay<T>(
            this IObservable<T> source,
            int retryCount,
            Func<int, TimeSpan> getDelay)
        {
            var attempt = 0;
            return Observable.Defer(() => source)
                .Catch<T, Exception>(ex =>
                {
                    if (attempt >= retryCount)
                    {
                        return Observable.Throw<T>(ex);
                    }

                    return Observable.Timer(getDelay(attempt++))
                        .SelectMany(_ => Observable.Defer(() => source));
                })
                .Retry(retryCount);
        }
    }

    public class UnspecifiedExceptionOccurred : Exception
    {
    }
}