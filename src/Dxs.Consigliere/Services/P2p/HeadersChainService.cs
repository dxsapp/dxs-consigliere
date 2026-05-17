#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
using Dxs.Bsv.P2p.Session;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.WebSockets;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 1 S3 hosted service. Drives the headers chain off the live P2P pool:
///
/// - On startup loads any persisted headers via <see cref="BlockHeaderStore"/>
///   and replays them into the in-memory <see cref="HeadersChain"/>.
/// - Attaches <see cref="PeerSession.OnHeadersReceived"/> /
///   <see cref="PeerSession.OnInvReceived"/> callbacks to every peer that
///   reaches <see cref="PeerSessionState.Ready"/>. Callbacks are additive
///   (frozen by S0) so the existing TxRelayCoordinator path keeps working.
/// - On <c>inv(MSG_BLOCK)</c> sends <c>getheaders</c> to the announcing peer.
/// - On <c>headers</c> feeds each header into <see cref="HeadersChain.TryExtend"/>;
///   <see cref="ExtendResult.Extended"/> / <see cref="ExtendResult.Fork"/>
///   are persisted; <see cref="ExtendResult.Extended"/> additionally fires
///   the <see cref="INewBlockNotifier"/> (consumed by S5's SignalR pump).
/// - A periodic timer sends a baseline <c>getheaders</c> to one ready peer
///   per cycle so missed tip notifications still surface within
///   <see cref="HeadersChainOptions.GetHeadersIntervalMs"/>.
///
/// Wave 3 owns reorg recovery — this service only DETECTS a divergence
/// (Fork outcome) and persists the competing tip candidate.
/// </summary>
public sealed class HeadersChainService : IHostedService, IAsyncDisposable
{
    private readonly BsvP2pHealth _health;
    private readonly HeadersChain _chain;
    private readonly HeadersChainOptions _options;
    private readonly IBlockHeaderStore _store;
    private readonly INewBlockNotifier _notifier;
    private readonly HeadersChainBootstrapper _bootstrapper;
    private readonly BsvP2pConfig _p2pConfig;
    private readonly IReorgPipeline? _reorgPipeline;
    private readonly ILogger<HeadersChainService> _logger;

    // Sessions we've already wired callbacks for; tracked by reference so
    // gc-collected sessions drop out naturally when the dictionary entry
    // is reaped on the next reconcile.
    private readonly HashSet<PeerSession> _wired = new(ReferenceEqualityComparer.Instance);

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public HeadersChainService(
        BsvP2pHealth health,
        HeadersChain chain,
        IOptions<HeadersChainOptions> options,
        IBlockHeaderStore store,
        INewBlockNotifier notifier,
        HeadersChainBootstrapper bootstrapper,
        IOptions<BsvP2pConfig> p2pOptions,
        ILogger<HeadersChainService> logger,
        IReorgPipeline? reorgPipeline = null)
    {
        _health = health;
        _chain = chain;
        _options = options.Value;
        _store = store;
        _notifier = notifier;
        _bootstrapper = bootstrapper;
        _p2pConfig = p2pOptions.Value;
        _logger = logger;
        _reorgPipeline = reorgPipeline;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_p2pConfig.Enabled)
        {
            _logger.LogInformation("HeadersChainService disabled (P2P off).");
            return;
        }

        // Replay persisted headers into the in-memory chain.
        var persisted = await _store.RecentAsync(_options.RetainedHeaderCount, cancellationToken);
        if (persisted.Count > 0)
        {
            var seeds = persisted
                .Select(d => (new BlockHeader(d.HeaderBytes80), d.Height))
                .ToList();
            _chain.LoadFromStore(seeds);
            _logger.LogInformation("HeadersChainService replayed {Count} headers; tip height {Tip}",
                seeds.Count, _chain.TipHeight);
        }
        else
        {
            // Mark as loaded even when empty — distinguishes "fresh cold start"
            // from "store unavailable".
            _chain.LoadFromStore(Array.Empty<(BlockHeader, long)>());
        }

        // Optional warm-start bootstrap from an external source
        // (Bitails REST). No-op when SeedFromBitails=false or chain is
        // already populated. See HeadersChainBootstrapper for semantics.
        try { await _bootstrapper.BootstrapAsync(cancellationToken); }
        catch (Exception ex) { _logger.LogWarning(ex, "Bootstrap pass failed; continuing with pure-P2P cold start"); }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            try { _cts.Cancel(); } catch { }
        }
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); } catch { }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(1000, _options.GetHeadersIntervalMs));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Reconcile();
                await SendBaselineGetHeadersAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HeadersChainService tick failed");
            }

            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Attach callbacks to any new Ready sessions in the live pool.
    /// Sessions that drop fall out of the wired set so the references
    /// are released for GC.
    /// </summary>
    public void Reconcile()
    {
        if (!_health.Bound) return;
        Reconcile(_health.ActiveSessions);
    }

    /// <summary>
    /// Test seam: same logic against an explicit session collection.
    /// Production callers use the no-arg overload backed by
    /// <see cref="BsvP2pHealth.ActiveSessions"/>.
    /// </summary>
    public void Reconcile(IReadOnlyCollection<PeerSession> sessions)
    {
        foreach (var session in sessions.Where(s => s.State == PeerSessionState.Ready))
        {
            if (_wired.Contains(session)) continue;
            AttachCallbacks(session);
            _wired.Add(session);
            _logger.LogDebug("HeadersChainService wired callbacks for {Peer}", session.Remote);
        }
        var liveSet = new HashSet<PeerSession>(sessions, ReferenceEqualityComparer.Instance);
        _wired.RemoveWhere(s => !liveSet.Contains(s));
    }

    private async Task SendBaselineGetHeadersAsync(CancellationToken ct)
    {
        // Pick one ready peer and send getheaders so we close any gap
        // between inv(MSG_BLOCK) notifications. Cheap fallback — the
        // primary fast-path is the inv-driven HandleInvAsync below.
        if (!_health.Bound) return;
        var peer = _health.ActiveSessions.FirstOrDefault(s => s.State == PeerSessionState.Ready);
        if (peer is null) return;
        try
        {
            var msg = new GetHeadersMessage(70016, BuildLocator(), new byte[32]);
            await peer.SendGetHeadersAsync(msg, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Baseline getheaders failed");
        }
    }

    private void AttachCallbacks(PeerSession session)
    {
        // Compose with any previously-set delegate (audit S0 additive
        // invariant + standard .NET multicast on Action).
        var prevHeaders = session.OnHeadersReceived;
        session.OnHeadersReceived = prevHeaders is null
            ? (Action<IReadOnlyList<BlockHeader>>)(hdrs => _ = HandleHeadersAsync(hdrs))
            : prevHeaders + (hdrs => _ = HandleHeadersAsync(hdrs));

        var prevInv = session.OnInvReceived;
        Action<InvMessage> ourInv = inv => _ = HandleInvAsync(session, inv);
        session.OnInvReceived = prevInv is null ? ourInv : prevInv + ourInv;
    }

    private async Task HandleInvAsync(PeerSession session, InvMessage inv)
    {
        try
        {
            // We send getheaders if any item is MSG_BLOCK. Locator = our tip
            // (or zero-hash when empty) — sufficient as a request for the
            // peer's "what comes next" reply.
            if (!inv.Items.Any(i => i.Type == InvType.Block)) return;

            var locator = BuildLocator();
            var msg = new GetHeadersMessage(70016, locator, new byte[32]);
            await session.SendGetHeadersAsync(msg, CancellationToken.None);
            _logger.LogDebug("Sent getheaders to {Peer} (locator {Count})",
                session.Remote, locator.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HandleInvAsync failed for {Peer}", session.Remote);
        }
    }

    private async Task HandleHeadersAsync(IReadOnlyList<BlockHeader> headers)
    {
        foreach (var header in headers)
        {
            ExtendResult result;
            try { result = _chain.TryExtend(header); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TryExtend threw on header");
                continue;
            }

            switch (result)
            {
                case ExtendResult.Extended ext:
                    await PersistAsync(ext.Header, ext.Height);
                    await PruneIfNeededAsync();
                    await _notifier.NotifyAsync(BuildTipDto(ext.Header, ext.Height), CancellationToken.None);
                    break;
                case ExtendResult.Fork fork:
                    await PersistAsync(fork.Header, fork.ParentHeight + 1);
                    _logger.LogInformation("Stored fork header at height {H}", fork.ParentHeight + 1);
                    // Wave 3: hand the fork tip to the reorg pipeline.
                    // Optional dependency — null in some test setups; in
                    // production DI it's always registered via
                    // BsvP2pSetup.
                    if (_reorgPipeline is not null)
                    {
                        try
                        {
                            await _reorgPipeline.HandleForkObservedAsync(fork.Header, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "ReorgPipeline.HandleForkObservedAsync threw");
                        }
                    }
                    break;
                case ExtendResult.AlreadyKnown:
                    // silent
                    break;
                case ExtendResult.Orphan:
                    _logger.LogDebug("Orphan header observed; missing parent");
                    break;
                case ExtendResult.Unanchored:
                    // Audit A2 H1: chain has no height anchor yet (bootstrap
                    // failed or hasn't run). Don't persist arbitrary headers
                    // at height 0; wait for the bootstrapper to seed.
                    _logger.LogWarning("Header dropped: chain not yet anchored (bootstrap pending)");
                    break;
                case ExtendResult.Invalid bad:
                    _logger.LogWarning("Invalid header rejected: {Reason}", bad.Reason);
                    break;
            }
        }
    }

    private async Task PersistAsync(BlockHeader header, long height)
    {
        var hash = ToHexLower(BlockHeaderHasher.Hash(header));
        var prev = ToHexLower(BlockHeaderHasher.PrevBlock(header));
        var doc = new BlockHeaderDocument
        {
            Id = BlockHeaderDocument.BuildId(hash),
            Hash = hash,
            Height = height,
            PrevHash = prev,
            TimestampMs = (long)BlockHeaderHasher.TimestampUnixSeconds(header) * 1000L,
            HeaderBytes80 = header.Bytes80,
        };
        await _store.SaveAsync(doc, CancellationToken.None);
    }

    private async Task PruneIfNeededAsync()
    {
        if (_chain.TipHeight < _options.RetainedHeaderCount) return;
        var cutoff = _chain.TipHeight - _options.RetainedHeaderCount + 1;
        if (cutoff <= 0) return;
        await _store.PruneBelowAsync(cutoff, CancellationToken.None);
    }

    private List<byte[]> BuildLocator()
    {
        // Simple locator: our current tip if any, else empty list. Mainnet
        // peers accept this — they reply with headers building forward from
        // the locator's first known entry.
        var locator = new List<byte[]>();
        var tip = _chain.Tip;
        if (tip is not null)
            locator.Add(BlockHeaderHasher.Hash(tip));
        return locator;
    }

    private static BlockTipDto BuildTipDto(BlockHeader header, long height)
    {
        // Audit A2 H3: external surface uses display-order hash so it
        // matches WhatsOnChain / Bitails / explorers byte-for-byte.
        // Internal Raven docs and chain linkage stay wire-order.
        return new BlockTipDto(
            Hash: BlockHeaderHasher.ToDisplayHex(BlockHeaderHasher.Hash(header)),
            Height: height,
            TimestampMs: (long)BlockHeaderHasher.TimestampUnixSeconds(header) * 1000L,
            PrevHash: BlockHeaderHasher.ToDisplayHex(BlockHeaderHasher.PrevBlock(header)),
            HeaderSize: BlockHeader.Size);
    }

    private static string ToHexLower(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(bytes).ToLowerInvariant();

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _cts?.Dispose();
    }
}
