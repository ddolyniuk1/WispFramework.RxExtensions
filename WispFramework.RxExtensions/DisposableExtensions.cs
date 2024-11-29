using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;

namespace WispFramework.RxExtensions
{
    public static class DisposableExtensions
    {
        public static void DisposeWith(this IDisposable disposable,
            CompositeDisposable compositeDisposable)
        {  
            compositeDisposable.Add(disposable);
        }

        public static void DisposeWith(this IDisposable disposable, SerialDisposable serialDisposable)
        {
            serialDisposable.Disposable = disposable;
        }
    }
}
