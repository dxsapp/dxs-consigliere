using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Dxs.Common.Extensions;

public static class ObservableExtensions
{
    private static readonly Action<Exception> IgnoreError = _ => { };

    private static IObservable<T> Handle<T>(this IObservable<T> tasks, Action<Exception> onError) =>
        tasks.Do(_ => { }, onError ?? IgnoreError);

    private static IObservable<Unit> SelectAsync<T>(this IObservable<T> source, Func<T, Task> onNext) =>
        source
            .SelectMany(e => onNext(e).ToObservable())
            .Synchronize();

    /// <summary>
    /// Restarts subscription in case of exception.
    /// If <paramref name="onError"/> is not <c>null</c> it is executed before retry.
    /// </summary>
    public static IObservable<TSource> Retry<TSource>(this IObservable<TSource> source, Action<Exception> onError) =>
        source
            .Handle(onError)
            .Retry();

    /// <summary>
    /// Subscribes and retries subscription in case of exception.
    /// If <paramref name="onError"/> is not <c>null</c> it is executed before retry.
    /// </summary>
    public static IDisposable SubscribeRetry<T>(this IObservable<T> source, Action<T> onNext, Action<Exception> onError = null) =>
        source
            .Do(onNext)
            .Retry(onError)
            .Subscribe();

    /// <summary>
    /// Subscribes using asynchronous <paramref name="onNext"/> handler.
    /// ALl invocations of <paramref name="onNext"/> are guaranteed to be done consequently (not in parallel).
    /// </summary>
    /// <remarks>
    /// WARNING This method can lead to high memory usage if invoked frequently.
    /// </remarks>
    public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNext, Action<Exception> onError = null) =>
        source
            .SelectAsync(onNext)
            .Handle(onError)
            .Subscribe();

    /// <summary>
    /// Subscribes using asynchronous <paramref name="onNext"/> handler and retries subscription in case of exception.
    /// ALl invocations of <paramref name="onNext"/> are guaranteed to be done consequently (not in parallel).
    /// If <paramref name="onError"/> is not <c>null</c> it is executed before retry.
    /// </summary>
    /// <remarks>
    /// WARNING This method can lead to high memory usage if invoked frequently.
    /// </remarks>
    public static IDisposable SubscribeRetryAsync<T>(this IObservable<T> source, Func<T, Task> onNext, Action<Exception> onError = null) =>
        source
            .SelectAsync(onNext)
            .Handle(onError)
            .Retry()
            .Subscribe();
}
