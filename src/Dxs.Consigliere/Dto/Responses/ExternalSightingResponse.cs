#nullable enable
namespace Dxs.Consigliere.Dto.Responses;

/// <summary>
/// Whether a public explorer (WhatsOnChain / Bitails / JungleBus) has seen a
/// given txid yet. Used by the Broadcast inspector to independently confirm —
/// for a demo — that a tx the thin node broadcast over its own P2P pool
/// actually propagated to third-party indexers. Polled per source until seen.
/// </summary>
public sealed class ExternalSightingResponse
{
    public string Source { get; set; } = string.Empty;
    public bool Seen { get; set; }
}
