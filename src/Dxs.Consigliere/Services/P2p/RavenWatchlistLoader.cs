#nullable enable
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv;
using Dxs.Bsv.P2p.Observer;
using Dxs.Consigliere.Data.Models;

using Microsoft.Extensions.Logging;

using Raven.Client.Documents;
using Raven.Client.Documents.Changes;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 2 S3 — bridge between <see cref="WatchingAddress"/> /
/// <see cref="WatchingToken"/> Raven documents and the in-memory
/// <see cref="WatchlistMatcher"/>.
///
/// <para>
/// On <see cref="InitializeAsync"/>: bulk-streams every
/// <see cref="WatchingAddress"/> + <see cref="WatchingToken"/>
/// document into the matcher, then opens Raven Changes API
/// subscriptions on both collections. Add / remove deltas are
/// pushed into the matcher.
/// </para>
/// <para>
/// Uses the **Raven Changes API** (not Subscription API) — same
/// pattern as
/// <c>src/Dxs.Consigliere/BackgroundTasks/StasAttributesChangeObserverTask.cs</c>.
/// Changes API supports both <c>Put</c> and <c>Delete</c> events
/// on a collection, which is what the watchlist remove path needs
/// (audit W2 H2).
/// </para>
/// <para>
/// Document ID conventions:
/// <list type="bullet">
/// <item><c>WatchingAddress.Id = "address/{Address}"</c> — strip
///   the <c>"address/"</c> prefix on Delete to recover the address.</item>
/// <item><c>WatchingToken.Id = "token/{TokenId}/{Symbol}"</c> —
///   parse the middle segment on Delete to recover the token id.</item>
/// </list>
/// </para>
/// </summary>
public sealed class RavenWatchlistLoader : IAsyncDisposable
{
    private const string AddressIdPrefix = "address/";
    private const string TokenIdPrefix = "token/";

    private readonly IDocumentStore _store;
    private readonly WatchlistMatcher _matcher;
    private readonly ILogger<RavenWatchlistLoader> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _subscriptionLoop;
    private int _disposed;

    public RavenWatchlistLoader(
        IDocumentStore store,
        WatchlistMatcher matcher,
        ILogger<RavenWatchlistLoader> logger)
    {
        _store = store;
        _matcher = matcher;
        _logger = logger;
    }

    /// <summary>The matcher this loader feeds. Exposed for tests and
    /// for the mempool runner (S5) to consume.</summary>
    public WatchlistMatcher Matcher => _matcher;

    /// <summary>True once <see cref="InitializeAsync"/> has completed
    /// the initial bulk load. The mempool runner waits on this
    /// before processing observed tx (per S5 spec).</summary>
    public bool IsLoaded => _matcher.IsLoaded;

    /// <summary>
    /// Bulk-load the current watchlist and start the Changes
    /// subscription loop. Returns once the bulk load completes; the
    /// subscription continues in a background task until
    /// <see cref="DisposeAsync"/>.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var initLinked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        await BulkLoadAddressesAsync(initLinked.Token);
        await BulkLoadTokensAsync(initLinked.Token);

        _matcher.MarkLoaded();
        _logger.LogInformation(
            "RavenWatchlistLoader initial load complete: {AddrCount} addresses, {TokenCount} tokens",
            _matcher.WatchedAddressCount,
            _matcher.WatchedTokenCount);

        _subscriptionLoop = Task.Run(() => SubscriptionLoopAsync(_cts.Token), CancellationToken.None);
    }

    private async Task BulkLoadAddressesAsync(CancellationToken ct)
    {
        using var session = _store.OpenAsyncSession();
        var query = session.Advanced.AsyncRawQuery<WatchingAddress>("from WatchingAddresses");
        await using var stream = await session.Advanced.StreamAsync(query, ct);
        while (await stream.MoveNextAsync())
        {
            ApplyAddressAdd(stream.Current.Document);
        }
    }

    private async Task BulkLoadTokensAsync(CancellationToken ct)
    {
        using var session = _store.OpenAsyncSession();
        var query = session.Advanced.AsyncRawQuery<WatchingToken>("from WatchingTokens");
        await using var stream = await session.Advanced.StreamAsync(query, ct);
        while (await stream.MoveNextAsync())
        {
            ApplyTokenAdd(stream.Current.Document);
        }
    }

    private async Task SubscriptionLoopAsync(CancellationToken ct)
    {
        var addrCollection = _store.Conventions.FindCollectionName(typeof(WatchingAddress));
        var tokCollection = _store.Conventions.FindCollectionName(typeof(WatchingToken));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var changes = _store.Changes();
                using var addrSub = changes
                    .ForDocumentsInCollection(addrCollection)
                    .Where(c => c.Type is DocumentChangeTypes.Put or DocumentChangeTypes.Delete)
                    .Subscribe(HandleAddressChange);
                using var tokSub = changes
                    .ForDocumentsInCollection(tokCollection)
                    .Where(c => c.Type is DocumentChangeTypes.Put or DocumentChangeTypes.Delete)
                    .Subscribe(HandleTokenChange);

                // Keep both subscriptions alive until cancellation.
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RavenWatchlistLoader subscription error; retrying in 5s");
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private void HandleAddressChange(DocumentChange change)
    {
        try
        {
            if (change.Type == DocumentChangeTypes.Delete)
            {
                if (TryExtractAddress(change.Id, out var address))
                    ApplyAddressRemove(address);
                return;
            }

            // Put — load the doc + add to matcher. Fire-and-forget so
            // the subscription thread isn't blocked on a DB round trip.
            _ = Task.Run(async () =>
            {
                try
                {
                    using var session = _store.OpenAsyncSession();
                    var doc = await session.LoadAsync<WatchingAddress>(change.Id);
                    if (doc is not null) ApplyAddressAdd(doc);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load WatchingAddress {Id} on Put", change.Id);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RavenWatchlistLoader: error handling WatchingAddress change {Id}", change.Id);
        }
    }

    private void HandleTokenChange(DocumentChange change)
    {
        try
        {
            if (change.Type == DocumentChangeTypes.Delete)
            {
                if (TryExtractTokenId(change.Id, out var tokenId))
                    _matcher.RemoveToken(tokenId);
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var session = _store.OpenAsyncSession();
                    var doc = await session.LoadAsync<WatchingToken>(change.Id);
                    if (doc is not null) ApplyTokenAdd(doc);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load WatchingToken {Id} on Put", change.Id);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RavenWatchlistLoader: error handling WatchingToken change {Id}", change.Id);
        }
    }

    private void ApplyAddressAdd(WatchingAddress doc)
    {
        if (doc is null || string.IsNullOrEmpty(doc.Address)) return;
        try
        {
            var addr = new Address(doc.Address);
            _matcher.AddAddress(addr.Hash160);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Skipping malformed WatchingAddress {Addr}", doc.Address);
        }
    }

    private void ApplyAddressRemove(string address)
    {
        try
        {
            var addr = new Address(address);
            _matcher.RemoveAddress(addr.Hash160);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Skipping malformed address-delete {Addr}", address);
        }
    }

    private void ApplyTokenAdd(WatchingToken doc)
    {
        if (doc is null || string.IsNullOrEmpty(doc.TokenId)) return;
        _matcher.AddToken(doc.TokenId);
    }

    /// <summary>
    /// Parse <c>"address/{addr}"</c> form back into the bare
    /// address string. Public for tests + so the runner can sanity-
    /// check the convention.
    /// </summary>
    public static bool TryExtractAddress(string docId, out string address)
    {
        address = string.Empty;
        if (string.IsNullOrEmpty(docId)) return false;
        if (!docId.StartsWith(AddressIdPrefix, StringComparison.Ordinal)) return false;
        address = docId[AddressIdPrefix.Length..];
        return !string.IsNullOrEmpty(address);
    }

    /// <summary>
    /// Parse <c>"token/{tokenId}/{symbol}"</c> form back into the
    /// bare token id (middle segment). Symbol is ignored for
    /// matching purposes.
    /// </summary>
    public static bool TryExtractTokenId(string docId, out string tokenId)
    {
        tokenId = string.Empty;
        if (string.IsNullOrEmpty(docId)) return false;
        if (!docId.StartsWith(TokenIdPrefix, StringComparison.Ordinal)) return false;
        var rest = docId[TokenIdPrefix.Length..];
        var slashIdx = rest.IndexOf('/');
        if (slashIdx < 0)
        {
            // No symbol segment — treat the whole rest as token id.
            tokenId = rest;
            return !string.IsNullOrEmpty(tokenId);
        }
        tokenId = rest[..slashIdx];
        return !string.IsNullOrEmpty(tokenId);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _cts.Cancel(); } catch { }
        if (_subscriptionLoop is not null)
        {
            try { await _subscriptionLoop; } catch { /* shutdown */ }
        }
        _cts.Dispose();
    }
}
