using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reactive.Disposables;

namespace WispFramework.RxExtensions
{
    public static class PropertyChangedExtensions
    {
        // Cache compiled expressions
        private static readonly Dictionary<string, Delegate> ExpressionCache = new();

        public static IObservable<TProperty> ObserveProperty<TSource, TProperty>(
            this TSource source,
            Expression<Func<TSource, TProperty>> propertyExpression)
            where TSource : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyExpression == null) throw new ArgumentNullException(nameof(propertyExpression));

            var cacheKey = $"{typeof(TSource).FullName}_{typeof(TProperty).FullName}_{propertyExpression}";

            // Get or create cached getter
            var getter = GetCachedGetter(propertyExpression, cacheKey);

            // Get property path once
            var propertyPath = GetPropertyPath(propertyExpression);
            var propertyPathSet = new HashSet<string>(propertyPath); // For faster lookups

            return Observable.Create<TProperty>(observer =>
            {
                var subscriptions = new List<IDisposable>(propertyPath.Count); // Pre-size list
                var currentValue = default(TProperty);
                var isFirstRun = true;

                void EmitValue()
                {
                    var value = GetValueSafely(source, getter);
                    if (isFirstRun || !EqualityComparer<TProperty>.Default.Equals(value, currentValue))
                    {
                        currentValue = value;
                        observer.OnNext(value);
                        isFirstRun = false;
                    }
                }

                void SetupSubscriptions()
                {
                    var current = source as INotifyPropertyChanged;
                    var depth = 0;

                    while (current != null && depth < propertyPath.Count)
                    {
                        var obj = current;
                        var propertyName = propertyPath[depth];

                        subscriptions.Add(
                            Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                                    h => obj.PropertyChanged += h,
                                    h => obj.PropertyChanged -= h)
                                .Where(x => string.IsNullOrEmpty(x.EventArgs.PropertyName) ||
                                          propertyPathSet.Contains(x.EventArgs.PropertyName))
                                .Subscribe(_ => EmitValue())
                        );

                        if (++depth >= propertyPath.Count) break;

                        var property = obj.GetType().GetProperty(propertyName);
                        current = property?.GetValue(obj) as INotifyPropertyChanged;
                    }
                }

                SetupSubscriptions();
                EmitValue();

                return new CompositeDisposable(subscriptions);
            });
        }

        private static Func<TSource, TProperty> GetCachedGetter<TSource, TProperty>(
            Expression<Func<TSource, TProperty>> propertyExpression,
            string cacheKey)
        {
            if (!ExpressionCache.TryGetValue(cacheKey, out var cachedGetter))
            {
                cachedGetter = propertyExpression.Compile();
                ExpressionCache[cacheKey] = cachedGetter;
            }
            return (Func<TSource, TProperty>)cachedGetter;
        }

        private static List<string> GetPropertyPath<TSource, TProperty>(
            Expression<Func<TSource, TProperty>> propertyExpression)
        {
            var propertyPath = new List<string>();
            var expression = propertyExpression.Body;
            while (expression is MemberExpression memberExpression)
            {
                propertyPath.Add(memberExpression.Member.Name);
                expression = memberExpression.Expression;
            }
            propertyPath.Reverse();
            return propertyPath;
        }

        private static TProperty GetValueSafely<TSource, TProperty>(
            TSource source,
            Func<TSource, TProperty> getter)
        {
            try
            {
                return getter(source);
            }
            catch (NullReferenceException)
            {
                return default;
            }
        }
    }
}