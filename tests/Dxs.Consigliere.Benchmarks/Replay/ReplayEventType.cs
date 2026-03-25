namespace Dxs.Consigliere.Benchmarks.Replay;

public enum ReplayEventType
{
    TxSeenBySource,
    TxSeenInMempool,
    TxSeenInBlock,
    TxDroppedBySource,
    BlockConnected,
    BlockDisconnected,
}
