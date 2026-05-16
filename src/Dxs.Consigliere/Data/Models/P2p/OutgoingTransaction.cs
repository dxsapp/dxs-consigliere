using System;
using System.Collections.Generic;

namespace Dxs.Consigliere.Data.Models.P2p;

/// <summary>
/// Lifecycle record for one outgoing transaction submitted via BroadcastTracked.
/// Persisted in RavenDB collection "OutgoingTransactions".
/// </summary>
public sealed class OutgoingTransaction
{
    public string Id { get; set; }

    /// <summary>32-byte txid (hex, lowercase).</summary>
    public string TxId { get; set; }

    /// <summary>Raw transaction hex. Up to 2 MB for Phase 1.</summary>
    public string RawHex { get; set; }

    public int ParsedSizeBytes { get; set; }

    /// <summary>Satoshis per KB, computed from parsed tx.</summary>
    public long FeePerKbSat { get; set; }

    public OutgoingTxState State { get; set; } = OutgoingTxState.Submitted;

    public long CreatedAtMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdatedAtMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>SignalR ConnectionId of the submitting client (for targeted events).</summary>
    public string ClientConnectionId { get; set; }

    public List<PeerAttempt> PeerAttempts { get; set; } = new();

    public long? MempoolSeenAtMs { get; set; }
    public long? MinedAtMs { get; set; }
    public string BlockHash { get; set; }
    public int ConfirmationCount { get; set; }

    public int RetryCount { get; set; }
    public long? NextRetryAtMs { get; set; }

    public string LastError { get; set; }
    public string TerminalReason { get; set; }

    public static string BuildId(string txId) => $"outgoing-tx/{txId}";

    public void Touch() => UpdatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

public sealed class PeerAttempt
{
    public string PeerEndpoint { get; set; }
    public long AnnouncedAtMs { get; set; }
    public long? GetDataServedAtMs { get; set; }
    public long? RelayBackAtMs { get; set; }
    public string RejectCode { get; set; }
    public string RejectReason { get; set; }
    public string RejectClass { get; set; }
}
