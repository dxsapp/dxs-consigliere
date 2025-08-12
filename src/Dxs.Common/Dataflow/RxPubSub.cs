using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Dxs.Common.Dataflow;

public class RxPubSub<T>: IDisposable, IObservable<T>
{
    private readonly ISubject<T> _subject = new Subject<T>();
    private readonly List<IObserver<T>> _observers = new();
    private readonly List<IDisposable> _subscriptions = new();

    public IDisposable Subscribe(IObserver<T> observer)
    {
        _observers.Add(observer);
        _subscriptions.Add(_subject.Subscribe(observer));

        return new ObserverHandler<T>(observer, _observers);
    }

    public IDisposable AddPublisher(IObservable<T> observable)
        => observable.Subscribe(_subject);

    public IObservable<T> AsObservable() => _subject.AsObservable();

    public IObserver<T> AsObserver() => _subject.AsObserver();

    public void Post(T value) => _subject.OnNext(value);

    public virtual void Dispose()
    {
        _subject.OnCompleted();
        _observers.ForEach(x => x.OnCompleted());
        _observers.Clear();

        _subscriptions.ForEach(x => x.Dispose());
        _subscriptions.Clear();
    }
}