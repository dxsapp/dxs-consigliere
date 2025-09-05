using Dxs.Bsv.Models;

namespace Dxs.Bsv.BitcoinMonitor.Models;

public readonly struct TxMessage
{
    public enum Type
    {
        Manual,
        AddedToMempool,
        FoundInBlock,
        RemoveTransaction
    }

    private TxMessage(
        Type messageType,
        Transaction transaction,
        string txId,
        long timestamp,
        string blockHash = default,
        int height = int.MaxValue,
        int idx = 0,
        RemoveFromMempoolReason reason = default,
        string collidedWithTransaction = default
    ) => (MessageType, Transaction, TxId, Timestamp, BlockHash, Height, Idx, Reason, CollidedWithTransaction)
        = (messageType, transaction, txId, timestamp, blockHash, height, idx, reason, collidedWithTransaction);

    public Type MessageType { get; }
    public Transaction Transaction { get; }
    public string TxId { get; }
    public long Timestamp { get; }
    public string BlockHash { get; }
    public int Height { get; }
    public int Idx { get; }
    public RemoveFromMempoolReason Reason { get; }
    public string CollidedWithTransaction { get; }

    #region Factory

    public static TxMessage AddedToMempool(Transaction transaction, long timestamp)
        => new(Type.AddedToMempool, transaction, transaction.Id, timestamp);

    public static TxMessage FoundInBlock(
        Transaction transaction,
        long timestamp,
        string blockHash,
        int height,
        int idx
    )
        => new(Type.FoundInBlock, transaction, transaction.Id, timestamp, blockHash, height, idx);

    public static TxMessage RemovedFromMempool(
        string txId,
        RemoveFromMempoolReason reason,
        string collidedWithTransaction,
        string blockHash
    ) => new(Type.RemoveTransaction, null, txId, default, blockHash, int.MaxValue, 0, reason, collidedWithTransaction);

    public static TxMessage RemovedFromMempool(
        string txId,
        RemoveFromMempoolReason reason
    ) => RemovedFromMempool(txId, reason, default, default);

    #endregion

}