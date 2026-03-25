using System;

namespace Dxs.Bsv.BitcoinMonitor.Models;

public static class TxObservationEventType
{
    public const string SeenInMempool = "tx_seen_in_mempool";
    public const string SeenInBlock = "tx_seen_in_block";
    public const string DroppedBySource = "tx_dropped_by_source";
}

public static class TxObservationSource
{
    public const string Node = "node";
    public const string JungleBus = "junglebus";
}

public sealed record TxObservation(
    string EventType,
    string Source,
    string TxId,
    DateTimeOffset? ObservedAt = null,
    string BlockHash = null,
    int? BlockHeight = null,
    int? TransactionIndex = null,
    string RemoveReason = null,
    string CollidedWithTransaction = null
);
