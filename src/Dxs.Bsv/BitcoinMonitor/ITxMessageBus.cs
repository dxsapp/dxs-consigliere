using System;

using Dxs.Bsv.BitcoinMonitor.Models;

namespace Dxs.Bsv.BitcoinMonitor;

public interface ITxMessageBus : IObservable<TxMessage>
{
    IDisposable AddPublisher(IObservable<TxMessage> txObservable);
    IObservable<TxMessage> AsObservable();
    IObserver<TxMessage> AsObserver();
    void Post(TxMessage message);
}
