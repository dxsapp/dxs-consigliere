using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.BitcoinMonitor.Models;

namespace Dxs.Bsv.BitcoinMonitor;

public interface ITxObservationSink
{
    Task RecordAsync(TxMessage message, CancellationToken cancellationToken = default);
}

public sealed class NullTxObservationSink : ITxObservationSink
{
    public Task RecordAsync(TxMessage message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
