namespace Dxs.Bsv.BitcoinMonitor.Models;

public enum RemoveFromMempoolReason
{
    Unknown,
    Expired,
    MempoolSizeLimitExceeded,
    CollisionInBlockTx,
    Reorg,
    IncludedInBlock
}
