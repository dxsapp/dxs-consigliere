using System;

using Dxs.Bsv.BitcoinMonitor.Models;

namespace Dxs.Bsv.BitcoinMonitor;

public interface IBlockMessageBus : IObservable<BlockMessage>
{
    IDisposable AddPublisher(IObservable<BlockMessage> txObservable);
    IObservable<BlockMessage> AsObservable();
    IObserver<BlockMessage> AsObserver();
    void Post(BlockMessage message);
}
