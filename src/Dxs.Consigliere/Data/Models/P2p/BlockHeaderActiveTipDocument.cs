namespace Dxs.Consigliere.Data.Models.P2p;

/// <summary>
/// Audit W3 A2-followup N1 — single-row Raven document holding the
/// persisted active-tip hash + height. Read on startup so a
/// previously-rejected fork (taller in raw-Height ORDER BY but
/// rejected by the cumulative-work comparator) cannot become active
/// after restart. Written on every successful promotion: the initial
/// extend case in <c>HeadersChainService.PersistAsync</c> AND the
/// reorg promotion case in <c>ReorgPipeline</c>'s
/// <c>promote-fork</c> step. Fork headers themselves remain
/// persisted (under <see cref="BlockHeaderDocument"/>) for ancestor
/// walks; only the active-tip pointer document gates startup
/// selection.
/// </summary>
public sealed class BlockHeaderActiveTipDocument
{
    public const string DocumentId = "block-headers/active-tip";

    public string Id { get; set; } = DocumentId;
    public string BlockHashHex { get; set; } = string.Empty;
    public long Height { get; set; }
}
