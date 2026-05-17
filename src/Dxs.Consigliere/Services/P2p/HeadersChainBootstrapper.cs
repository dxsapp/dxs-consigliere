#nullable enable
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 1 S4. Required cold-start step: anchor the headers chain to
/// the authoritative current mainnet tip from a non-P2P source so
/// W2/W6 see a real height, not a fabricated height 0.
///
/// The bootstrapper is gated by
/// <see cref="HeadersChainOptions.SeedFromBitails"/>. When disabled,
/// the chain stays <see cref="ExtendResult.Unanchored"/> on cold
/// start; arbitrary P2P headers arriving in that state are dropped
/// (fail-closed semantics from audit A2 H1) rather than promoted to
/// height 0.
///
/// The bootstrap source is abstracted behind
/// <see cref="IHeadersBootstrapSource"/>. W1 production ships
/// <see cref="WhatsOnChainHeadersBootstrapSource"/> as the default DI
/// binding (audit A2 followup new-H1); it fetches the live tip from
/// WhatsOnChain's chain-info + block-header endpoints and reconstructs
/// the 80-byte header from the JSON fields.
/// <see cref="NoopHeadersBootstrapSource"/> remains available for
/// tests / non-production hosts that intentionally start cold and
/// will manually <see cref="HeadersChain.Seed"/>.
/// </summary>
public sealed class HeadersChainBootstrapper(
    HeadersChain chain,
    IBlockHeaderStore store,
    IHeadersBootstrapSource source,
    IOptions<HeadersChainOptions> options,
    ILogger<HeadersChainBootstrapper> logger)
{
    private readonly HeadersChainOptions _options = options.Value;

    /// <summary>
    /// Run once on service start. Returns true when a seed was applied,
    /// false when bootstrap was skipped (chain already populated, or
    /// SeedFromBitails=false, or source returned no data).
    /// </summary>
    public async Task<bool> BootstrapAsync(CancellationToken ct)
    {
        if (!_options.SeedFromBitails)
        {
            logger.LogDebug("Headers bootstrap skipped (SeedFromBitails=false)");
            return false;
        }
        if (chain.Tip is not null)
        {
            logger.LogDebug("Headers bootstrap skipped (chain already has tip {H})", chain.TipHeight);
            return false;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(_options.BootstrapTimeoutMs);

        BootstrapSeed? seed;
        try
        {
            seed = await source.FetchAsync(deadline.Token);
        }
        catch (System.OperationCanceledException) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Headers bootstrap timed out after {Ms}ms; pure-P2P will take over", _options.BootstrapTimeoutMs);
            return false;
        }
        catch (System.Exception ex)
        {
            logger.LogWarning(ex, "Headers bootstrap source failed; pure-P2P will take over");
            return false;
        }

        if (seed is null || seed.HeaderBytes80.Length != BlockHeader.Size)
        {
            logger.LogDebug("Headers bootstrap source returned no usable seed");
            return false;
        }

        var header = new BlockHeader(seed.HeaderBytes80);
        var hash = System.Convert.ToHexString(BlockHeaderHasher.Hash(header)).ToLowerInvariant();
        var prev = System.Convert.ToHexString(BlockHeaderHasher.PrevBlock(header)).ToLowerInvariant();
        var doc = new BlockHeaderDocument
        {
            Id = BlockHeaderDocument.BuildId(hash),
            Hash = hash,
            Height = seed.Height,
            PrevHash = prev,
            TimestampMs = (long)BlockHeaderHasher.TimestampUnixSeconds(header) * 1000L,
            HeaderBytes80 = seed.HeaderBytes80,
        };
        await store.SaveAsync(doc, ct);
        // Audit A2 H1: use explicit Seed(...) to anchor the chain. The
        // first P2P header now requires this anchor; otherwise it returns
        // Unanchored and gets dropped.
        chain.Seed(header, seed.Height);

        logger.LogInformation("Headers chain bootstrapped from external source: height={H} hash={Hash}",
            seed.Height, hash);
        return true;
    }
}

/// <summary>
/// One header + height returned by a bootstrap source. The 80-byte
/// header is the canonical wire representation; height is the chain
/// height of the tip.
/// </summary>
public sealed record BootstrapSeed(byte[] HeaderBytes80, long Height);

/// <summary>
/// Pluggable source of bootstrap data. W1 production binds
/// <see cref="WhatsOnChainHeadersBootstrapSource"/> as the default
/// implementation (audit A2 followup new-H1).
/// <see cref="NoopHeadersBootstrapSource"/> remains available for
/// tests that want to start cold and inject anchor state manually.
/// </summary>
public interface IHeadersBootstrapSource
{
    Task<BootstrapSeed?> FetchAsync(CancellationToken ct);
}

/// <summary>
/// No-op source. Not the production default — production uses
/// <see cref="WhatsOnChainHeadersBootstrapSource"/>. Used by tests
/// and by environments that intentionally start cold (the chain
/// then stays <see cref="ExtendResult.Unanchored"/> until
/// <see cref="HeadersChain.Seed"/> is called explicitly).
/// </summary>
public sealed class NoopHeadersBootstrapSource : IHeadersBootstrapSource
{
    public Task<BootstrapSeed?> FetchAsync(CancellationToken ct) => Task.FromResult<BootstrapSeed?>(null);
}
