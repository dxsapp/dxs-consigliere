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
/// Wave 1 S4. Cold-start convenience: pre-populate the headers chain
/// from a non-P2P source (e.g. Bitails REST `/chain/info`) so admin
/// endpoints have a tip immediately, instead of waiting for the first
/// P2P inv/headers.
///
/// The bootstrapper is opt-in via
/// <see cref="HeadersChainOptions.SeedFromBitails"/>. When disabled,
/// the chain stays empty on cold start; the first P2P signal populates
/// it (typically &lt; 30 s on a healthy pool, capped by the
/// GetHeadersIntervalMs baseline tick).
///
/// The actual Bitails integration is intentionally abstracted behind
/// <see cref="IHeadersBootstrapSource"/>. W1 ships
/// <see cref="NoopHeadersBootstrapSource"/> as the default DI binding —
/// operators wire in a Bitails-backed source via a small REST shim when
/// they need warm-start behavior. Pure-P2P cold start (the W1 happy
/// path) works without any source.
/// </summary>
public sealed class HeadersChainBootstrapper(
    HeadersChain chain,
    BlockHeaderStore store,
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
        chain.LoadFromStore(new[] { (header, seed.Height) });

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
/// Pluggable source of bootstrap data. The W1 default
/// (<see cref="NoopHeadersBootstrapSource"/>) returns no seed; a
/// Bitails REST adapter can be substituted when warm-start matters.
/// </summary>
public interface IHeadersBootstrapSource
{
    Task<BootstrapSeed?> FetchAsync(CancellationToken ct);
}

/// <summary>Default no-op source; pure-P2P cold start is the supported W1 path.</summary>
public sealed class NoopHeadersBootstrapSource : IHeadersBootstrapSource
{
    public Task<BootstrapSeed?> FetchAsync(CancellationToken ct) => Task.FromResult<BootstrapSeed?>(null);
}
