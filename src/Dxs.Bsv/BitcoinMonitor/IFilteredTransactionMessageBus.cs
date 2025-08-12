using System;
using Dxs.Bsv.BitcoinMonitor.Models;

namespace Dxs.Bsv.BitcoinMonitor;

public interface IFilteredTransactionMessageBus : IObservable<FilteredTransactionMessage>
{
    IDisposable AddPublisher(IObservable<FilteredTransactionMessage> txObservable);
    IObservable<FilteredTransactionMessage> AsObservable();
    IObserver<FilteredTransactionMessage> AsObserver();
    void Post(FilteredTransactionMessage message);
}