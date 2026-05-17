#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.P2p.Chain;

/// <summary>
/// Wave 3 S1 — output of <see cref="ReorgDetector.TryDetect"/>. All hashes
/// are <b>display-order</b> hex (the same convention used by block
/// explorers, by <see cref="Dxs.Bsv.BitcoinMonitor.Models.BlockObservation.BlockHash"/>,
/// and by the SignalR <see cref="Dxs.Consigliere.WebSockets.ReorgEventDto"/>).
/// The detector internally walks the chain in wire-order but converts to
/// display-order before returning.
/// </summary>
/// <param name="CommonAncestorHash">Display-hex of the common ancestor;
/// empty when <paramref name="IsDegraded"/> is true (fork point fell
/// below the retention window — we don't know it for sure).</param>
/// <param name="CommonAncestorHeight">Height of the common ancestor;
/// approximate (<c>tipHeight - retentionWindow</c>) when degraded.</param>
/// <param name="OrphanedHashes">Active-chain blocks above the common
/// ancestor, in disconnect order — newest first. Empty when
/// degraded.</param>
/// <param name="NewChainHashes">Fork-side blocks above the common
/// ancestor, in connect order — oldest first. Empty when
/// degraded.</param>
/// <param name="NewTipHash">Display-hex of the fork-side tip that
/// triggered the detection.</param>
/// <param name="NewTipHeight">Height of the fork tip.</param>
/// <param name="IsDegraded">True when the fork point falls below
/// <c>HeadersChain.RetainedHeaderCount</c> and we can't enumerate
/// orphans reliably. The emitter fires a single
/// <c>OnReorg(DegradedState = true)</c> and stops.</param>
public sealed record ReorgPlan(
    string                 CommonAncestorHash,
    long                   CommonAncestorHeight,
    IReadOnlyList<string>  OrphanedHashes,
    IReadOnlyList<string>  NewChainHashes,
    string                 NewTipHash,
    long                   NewTipHeight,
    bool                   IsDegraded);
