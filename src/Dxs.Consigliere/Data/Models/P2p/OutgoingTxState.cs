namespace Dxs.Consigliere.Data.Models.P2p;

/// <summary>
/// Lifecycle of an outgoing transaction submitted via BroadcastTracked.
/// Per consigliere-thin-node-design.md §7.1.
/// </summary>
public enum OutgoingTxState
{
    /// <summary>Client submitted; persisted, not yet validated.</summary>
    Submitted,

    /// <summary>Local policy validation passed; ready to dispatch.</summary>
    Validated,

    /// <summary>Announced to peers via inv; waiting for getdata + relay-back.</summary>
    Dispatching,

    /// <summary>At least one peer sent getdata and was served the tx bytes.</summary>
    PeerAcked,

    /// <summary>≥K peers sent inv(txid) back — tx is propagating.</summary>
    PeerRelayed,

    /// <summary>Independent mempool observer saw the txid.</summary>
    MempoolSeen,

    /// <summary>A block containing the txid was applied.</summary>
    Mined,

    /// <summary>Configured confirmation count reached.</summary>
    Confirmed,

    // ── Terminal failure states ──────────────────────────────────────────

    /// <summary>
    /// Local policy rejection (size > limit, fee below floor, malformed).
    /// Never broadcast.
    /// </summary>
    PolicyInvalid,

    /// <summary>
    /// ≥N peers rejected with invalid-script / bad-txns reason.
    /// Permanent — not retried.
    /// </summary>
    InvalidRejected,

    /// <summary>
    /// ≥N peers rejected with mempool-conflict / missing-inputs reason.
    /// Permanent — not retried.
    /// </summary>
    ConflictRejected,

    /// <summary>
    /// Mempool evicted the tx and re-broadcast did not get it back in.
    /// </summary>
    EvictedOrDropped,

    /// <summary>
    /// No mempool/block signal for an extended period; operator action required.
    /// </summary>
    ObserverUnknown,

    /// <summary>All retry attempts exhausted with no success.</summary>
    Failed,
}

public static class OutgoingTxStates
{
    public static bool IsTerminal(OutgoingTxState s) => s is
        OutgoingTxState.Confirmed or
        OutgoingTxState.PolicyInvalid or
        OutgoingTxState.InvalidRejected or
        OutgoingTxState.ConflictRejected or
        OutgoingTxState.Failed;

    public static bool RequiresDispatch(OutgoingTxState s) => s is
        OutgoingTxState.Validated or
        OutgoingTxState.Dispatching;

    /// <summary>
    /// Wave 5 A2 L3 fix: returns true for any state that means "the
    /// system is making forward progress on this tx" — used by the
    /// unconfirmed-tx re-broadcast monitor to decide whether a fresh
    /// <see cref="IBroadcastService.BroadcastAsync"/> attempt should
    /// be logged as success or failure. Includes the post-dispatch
    /// observation states (<see cref="OutgoingTxState.MempoolSeen"/>,
    /// <see cref="OutgoingTxState.Mined"/>, <see cref="OutgoingTxState.Confirmed"/>)
    /// because a duplicate submission can return an existing receipt
    /// already in those states.
    /// </summary>
    public static bool IsActiveOrAccepted(OutgoingTxState s) => s is
        OutgoingTxState.Validated or
        OutgoingTxState.Dispatching or
        OutgoingTxState.PeerAcked or
        OutgoingTxState.PeerRelayed or
        OutgoingTxState.MempoolSeen or
        OutgoingTxState.Mined or
        OutgoingTxState.Confirmed;
}
