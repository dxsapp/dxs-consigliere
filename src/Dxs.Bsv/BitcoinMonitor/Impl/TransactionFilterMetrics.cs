using System.Threading;

using Dxs.Bsv.BitcoinMonitor.Models;

namespace Dxs.Bsv.BitcoinMonitor.Impl;

internal sealed class TransactionFilterMetrics
{
    private int _transactionsCounter;
    private int _foundInMempoolCounter;
    private int _foundInBlockCounter;
    private int _updatedOnBlockConnectedCounter;
    private int _reFoundInMempoolCounter;
    private int _notModified;

    public void IncrementProcessed() => _transactionsCounter++;

    public void Observe(TransactionProcessStatus status)
    {
        switch (status)
        {
            case TransactionProcessStatus.FoundInMempool:
                _foundInMempoolCounter++;
                break;
            case TransactionProcessStatus.FoundInBlock:
                _foundInBlockCounter++;
                break;
            case TransactionProcessStatus.UpdatedOnBlockConnected:
                _updatedOnBlockConnectedCounter++;
                break;
            case TransactionProcessStatus.ReFoundInMempool:
                _reFoundInMempoolCounter++;
                break;
            case TransactionProcessStatus.NotModified:
                _notModified++;
                break;
        }
    }

    public object SnapshotAndReset() => new
    {
        All = Interlocked.Exchange(ref _transactionsCounter, 0),
        FoundInMempool = Interlocked.Exchange(ref _foundInMempoolCounter, 0),
        FoundInBlock = Interlocked.Exchange(ref _foundInBlockCounter, 0),
        UpdatedOnBlockConnected = Interlocked.Exchange(ref _updatedOnBlockConnectedCounter, 0),
        ReFoundInMempool = Interlocked.Exchange(ref _reFoundInMempoolCounter, 0),
        NotModified = Interlocked.Exchange(ref _notModified, 0),
    };
}
