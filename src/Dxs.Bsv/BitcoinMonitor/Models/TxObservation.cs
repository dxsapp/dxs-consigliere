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
    public const string Bitails = "bitails";

    /// <summary>
    /// Wave 2 S0: BSV P2P peer-to-peer observation source.
    /// Used when a transaction is observed via inv(MSG_TX) / getdata
    /// from a connected BSV peer rather than through a REST / WebSocket
    /// provider. Accumulated into
    /// <c>TxLifecycleProjectionDocument.SeenBySources</c> alongside
    /// <see cref="Bitails"/> / <see cref="JungleBus"/> when multiple
    /// sources observe the same txid.
    /// </summary>
    public const string P2p = "p2p";

    /// <summary>
    /// A transaction this node itself built and broadcast (the lab /
    /// operator broadcast path). The raw bytes are available locally at
    /// dispatch time, so the broadcast feeds them straight into the same
    /// match → SaveTransaction → journal ingest pipeline an observed
    /// mempool tx takes — otherwise a self-broadcast that pays/spends a
    /// watched address would never credit/debit its projection (the node
    /// dedupes its own tx when peers relay it back).
    /// </summary>
    public const string Self = "self";
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
