using System;
using System.Reactive.Disposables;

namespace TrustMargin.Common.Extensions
{
    public static class DisposableExtensions
    {
        public static bool TryDispose<T>(this T value)
        {
            if (value == null)
                return false;

            if (value is not IDisposable disposable)
                return false;

            disposable.Dispose();
            return true;
        }

        public static IDisposable AddToCompositeDisposable(this IDisposable disposable, CompositeDisposable compositeDisposable)
        {
            compositeDisposable.Add(disposable);
            return disposable;
        }
    }
}
