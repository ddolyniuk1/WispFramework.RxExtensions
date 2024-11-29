using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;


namespace WispFramework.RxExtensions
{
    public static class PropertyChainChangedExtensions
    {
        private static readonly ConcurrentDictionary<string, List<string>> PropertyPathCache = new();

        public static IObservable<TProperty?> ObservePropertyChain<TSource, TProperty>(
            this TSource source,
            Expression<Func<TSource, TProperty>> propertyExpression)
            where TSource : INotifyPropertyChanged
            where TProperty : struct
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (propertyExpression == null)
            {
                throw new ArgumentNullException(nameof(propertyExpression));
            }

            var expressionString = propertyExpression.ToString();
            var propertyPath = PropertyPathCache.GetOrAdd(expressionString, _ => GetPropertyPath(propertyExpression));
            var getter = CreateNullSafeGetter(propertyExpression);

            return Observable.Create<TProperty?>(observer =>
            {
                var subscriptions = new CompositeDisposable();
                TProperty? previousValue = null;
                var syncRoot = new object();
                var initialized = false;

                UpdateSubscriptions();
                EmitValue();

                return subscriptions;

                void EmitValue()
                {
                    lock (syncRoot)
                    {
                        var value = getter(source);
                        if (initialized && EqualityComparer<TProperty?>.Default.Equals(value, previousValue)) return;
                        previousValue = value;
                        observer.OnNext(value);
                        initialized = true;
                    }
                }

                void UpdateSubscriptions()
                {
                    lock (syncRoot)
                    {
                        subscriptions.Clear();
                        INotifyPropertyChanged current = source;
                        var propertyQueue = new Queue<string>(propertyPath);

                        while (current != null && propertyQueue.Count > 0)
                        {
                            var obj = current;
                            var propertyName = propertyQueue.Dequeue();

                            var propertyChanged = Observable
                                .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                                    h => obj.PropertyChanged += h,
                                    h => obj.PropertyChanged -= h)
                                .Where(x => string.IsNullOrEmpty(x.EventArgs.PropertyName) ||
                                            x.EventArgs.PropertyName == propertyName);

                            var subscription = propertyChanged
                                .Subscribe(_ =>
                                {
                                    EmitValue();
                                    if (propertyQueue.Count > 0)
                                    {
                                        UpdateSubscriptions();
                                    }
                                });

                            subscriptions.Add(subscription);

                            if (propertyQueue.Count <= 0) continue;
                            var property = obj.GetType().GetProperty(propertyName);
                            current = property?.GetValue(obj) as INotifyPropertyChanged;
                        }
                    }
                }
            });
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
        private static Func<TSource, TProperty?> CreateNullSafeGetter<TSource, TProperty>(
            Expression<Func<TSource, TProperty>> expression)
            where TProperty : struct
        {
            var nullCheckedExpression = AddNullChecks(expression.Body);
            var lambda = Expression.Lambda<Func<TSource, TProperty?>>(
                nullCheckedExpression,
                expression.Parameters);
            return lambda.Compile();
        }

        private static Expression AddNullChecks(Expression expression)
        {
            if (expression is not MemberExpression memberExpr) return expression;
            var innerExpression = AddNullChecks(memberExpr.Expression);
             
            var memberType = memberExpr.Type;
            var nullableType = memberType.IsValueType ?
                typeof(Nullable<>).MakeGenericType(memberType) :
                memberType;

            var nullCheck = Expression.Condition(
                Expression.Equal(innerExpression, Expression.Constant(null)),
                Expression.Constant(null, nullableType),
                Expression.Convert(
                    Expression.MakeMemberAccess(innerExpression, memberExpr.Member),
                    nullableType)
            );
            return nullCheck;
        }
        private static TProperty GetValueSafely<TSource, TProperty>(
            TSource source,
            Func<TSource, TProperty> getter)
        {
            try
            {
                var value = getter(source); 
                if (value is not null || typeof(TProperty).IsValueType)
                    return value;
                return default;
            }
            catch (NullReferenceException)
            {
                return default;
            }
            catch (ObjectDisposedException)
            {
                return default;
            }
        }
    }
}
