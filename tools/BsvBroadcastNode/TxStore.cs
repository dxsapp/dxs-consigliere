using System.Collections.Concurrent;

namespace BsvBroadcastNode;

public enum TxState
{
    Submitted,
    Dispatching,
    PeerAcked,    // ≥1 peer served getdata
    PeerRelayed,  // ≥2 peers sent relay-back inv
    Failed,
}

public sealed class TxRecord
{
    public string TxId { get; init; } = "";
    public string RawHex { get; init; } = "";
    public TxState State { get; set; } = TxState.Submitted;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Error { get; set; }
    public List<string> PeersServed { get; } = new();
    public int RelayBackCount { get; set; }

    public void Transition(TxState next, string? error = null)
    {
        State = next;
        UpdatedAt = DateTimeOffset.UtcNow;
        if (error is not null) Error = error;
    }
}

/// <summary>Thread-safe in-memory transaction registry.</summary>
public sealed class TxStore
{
    private readonly ConcurrentDictionary<string, TxRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAdd(TxRecord r) => _records.TryAdd(r.TxId, r);
    public TxRecord? Get(string txId) => _records.TryGetValue(txId, out var r) ? r : null;
    public IEnumerable<TxRecord> All => _records.Values;
}
