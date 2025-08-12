namespace Dxs.Common.Dataflow;

public class ObserverHandler<T>(IObserver<T> observer, List<IObserver<T>> observers) : IDisposable
{
    public void Dispose()
    {
        observer.OnCompleted();
        observers.Remove(observer);
    }
}