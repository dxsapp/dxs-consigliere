namespace Dxs.Bsv.BitcoinMonitor.Models;

public enum TransactionProcessStatus
{
    Unexpected,
    FoundInMempool,
    FoundInBlock,
    UpdatedOnBlockConnected,
    ReFoundInMempool,
    NotModified
}