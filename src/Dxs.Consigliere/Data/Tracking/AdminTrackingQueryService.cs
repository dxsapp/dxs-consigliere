using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Data.Tracking;

public sealed class AdminTrackingQueryService(
    IDocumentStore documentStore,
    TokenProjectionReader tokenProjectionReader) : IAdminTrackingQueryService
{
    public async Task<AdminTrackedAddressResponse[]> GetTrackedAddressesAsync(
        bool includeTombstoned = false,
        CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var tracked = await session.Query<TrackedAddressDocument>().ToListAsync(cancellationToken);
        var statuses = await session.Query<TrackedAddressStatusDocument>().ToListAsync(cancellationToken);
        var statusByAddress = statuses.ToDictionary(x => x.Address, StringComparer.Ordinal);

        return tracked
            .Select(document => BuildAddressResponse(document, statusByAddress.TryGetValue(document.Address, out var status) ? status : null))
            .Where(x => includeTombstoned || !x.IsTombstoned)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToArray();
    }

    public async Task<AdminTrackedTokenResponse[]> GetTrackedTokensAsync(
        bool includeTombstoned = false,
        CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var tracked = await session.Query<TrackedTokenDocument>().ToListAsync(cancellationToken);
        var statuses = await session.Query<TrackedTokenStatusDocument>().ToListAsync(cancellationToken);
        var statusByToken = statuses.ToDictionary(x => x.TokenId, StringComparer.Ordinal);

        return tracked
            .Select(document => BuildTokenResponse(document, statusByToken.TryGetValue(document.TokenId, out var status) ? status : null))
            .Where(x => includeTombstoned || !x.IsTombstoned)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToArray();
    }

    public async Task<AdminTrackedAddressResponse> GetTrackedAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), cancellationToken);
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), cancellationToken);
        var summary = tracked is null
            ? null
            : await BuildAddressSummaryAsync(address, cancellationToken);
        return tracked is null
            ? null
            : BuildAddressResponse(tracked, status, summary);
    }

    public async Task<AdminTrackedTokenResponse> GetTrackedTokenAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken);
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken);
        var summary = tracked is null
            ? null
            : await BuildTokenSummaryAsync(tokenId, cancellationToken);
        return tracked is null
            ? null
            : BuildTokenResponse(tracked, status, summary);
    }

    public async Task<AdminFindingResponse[]> GetFindingsAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        take = Math.Max(1, take);
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var addressStatuses = await session.Query<TrackedAddressStatusDocument>().ToListAsync(cancellationToken);
        var tokenStatuses = await session.Query<TrackedTokenStatusDocument>().ToListAsync(cancellationToken);
        var findings = new List<AdminFindingResponse>();

        foreach (var status in addressStatuses.Where(x => x.Tracked && !x.IsTombstoned))
        {
            if (!string.IsNullOrWhiteSpace(status.FailureReason))
            {
                findings.Add(new AdminFindingResponse
                {
                    EntityType = TrackedEntityType.Address,
                    EntityId = status.Address,
                    Code = "failure_reason",
                    Severity = "error",
                    Message = status.FailureReason,
                    ObservedAt = status.DegradedAt ?? status.UpdatedAt ?? status.CreatedAt
                });
            }
        }

        foreach (var status in tokenStatuses.Where(x => x.Tracked && !x.IsTombstoned))
        {
            if (!string.IsNullOrWhiteSpace(status.FailureReason))
            {
                findings.Add(new AdminFindingResponse
                {
                    EntityType = TrackedEntityType.Token,
                    EntityId = status.TokenId,
                    Code = "failure_reason",
                    Severity = "error",
                    Message = status.FailureReason,
                    ObservedAt = status.DegradedAt ?? status.UpdatedAt ?? status.CreatedAt
                });
            }

            foreach (var unknownRoot in status.HistorySecurity?.UnknownRootFindings ?? [])
            {
                findings.Add(new AdminFindingResponse
                {
                    EntityType = TrackedEntityType.Token,
                    EntityId = status.TokenId,
                    Code = status.HistorySecurity.BlockingUnknownRoot ? "blocking_unknown_root" : "unknown_root",
                    Severity = status.HistorySecurity.BlockingUnknownRoot ? "error" : "warning",
                    Message = unknownRoot,
                    ObservedAt = status.UpdatedAt ?? status.CreatedAt
                });
            }
        }

        return findings
            .OrderByDescending(x => x.ObservedAt ?? 0)
            .Take(take)
            .ToArray();
    }

    public async Task<AdminDashboardSummaryResponse> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var addresses = await GetTrackedAddressesAsync(includeTombstoned: true, cancellationToken);
        var tokens = await GetTrackedTokensAsync(includeTombstoned: true, cancellationToken);

        return new AdminDashboardSummaryResponse
        {
            ActiveAddressCount = addresses.Count(x => !x.IsTombstoned && x.Readiness.Tracked),
            ActiveTokenCount = tokens.Count(x => !x.IsTombstoned && x.Readiness.Tracked),
            TombstonedAddressCount = addresses.Count(x => x.IsTombstoned),
            TombstonedTokenCount = tokens.Count(x => x.IsTombstoned),
            DegradedAddressCount = addresses.Count(x => !x.IsTombstoned && x.Readiness.Degraded),
            DegradedTokenCount = tokens.Count(x => !x.IsTombstoned && x.Readiness.Degraded),
            BackfillingAddressCount = addresses.Count(x => !x.IsTombstoned && string.Equals(x.Readiness.History?.HistoryReadiness, TrackedEntityHistoryReadiness.BackfillingFullHistory, StringComparison.Ordinal)),
            BackfillingTokenCount = tokens.Count(x => !x.IsTombstoned && string.Equals(x.Readiness.History?.HistoryReadiness, TrackedEntityHistoryReadiness.BackfillingFullHistory, StringComparison.Ordinal)),
            FullHistoryLiveAddressCount = addresses.Count(x => !x.IsTombstoned && string.Equals(x.Readiness.History?.HistoryReadiness, TrackedEntityHistoryReadiness.FullHistoryLive, StringComparison.Ordinal)),
            FullHistoryLiveTokenCount = tokens.Count(x => !x.IsTombstoned && string.Equals(x.Readiness.History?.HistoryReadiness, TrackedEntityHistoryReadiness.FullHistoryLive, StringComparison.Ordinal)),
            UnknownRootFindingCount = tokens.Sum(x => x.Readiness.History?.RootedToken?.UnknownRootFindingCount ?? 0),
            BlockingUnknownRootTokenCount = tokens.Count(x => x.Readiness.History?.RootedToken?.BlockingUnknownRoot == true),
            FailureCount = addresses.Count(x => !string.IsNullOrWhiteSpace(x.FailureReason))
                           + tokens.Count(x => !string.IsNullOrWhiteSpace(x.FailureReason))
        };
    }

    private async Task<AdminTrackedAddressSummaryResponse> BuildAddressSummaryAsync(
        string address,
        CancellationToken cancellationToken)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();

        var bsvBalance = await session.LoadAsync<AddressBalanceProjectionDocument>(
            AddressBalanceProjectionDocument.GetId(address, null),
            cancellationToken);

        var tokenBalances = await session.Query<AddressBalanceProjectionDocument>()
            .Where(x => x.Address == address && x.TokenId != null)
            .OrderBy(x => x.TokenId)
            .Select(x => new AdminTrackedTokenBalanceSummaryResponse
            {
                TokenId = x.TokenId,
                Satoshis = x.Satoshis
            })
            .ToListAsync(cancellationToken);

        var bsvUtxoCount = await session.Query<AddressUtxoProjectionDocument>()
            .Where(x => x.Address == address && x.TokenId == null)
            .CountAsync(cancellationToken);
        var tokenUtxoCount = await session.Query<AddressUtxoProjectionDocument>()
            .Where(x => x.Address == address && x.TokenId != null)
            .CountAsync(cancellationToken);

        var applicationQuery = session.Query<AddressProjectionAppliedTransactionDocument>()
            .Where(x => x.Credits.Any(y => y.Address == address) || x.Debits.Any(y => y.Address == address));

        var transactionCount = await applicationQuery.CountAsync(cancellationToken);
        var first = await applicationQuery
            .OrderBy(x => x.Timestamp)
            .ThenBy(x => x.TxId)
            .Select(x => new ActivitySnapshot
            {
                Timestamp = x.Timestamp,
                Height = x.Height,
                LastSequence = x.LastSequence
            })
            .FirstOrDefaultAsync(cancellationToken);
        var last = await applicationQuery
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.TxId)
            .Select(x => new ActivitySnapshot
            {
                Timestamp = x.Timestamp,
                Height = x.Height,
                LastSequence = x.LastSequence
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminTrackedAddressSummaryResponse
        {
            CurrentBsvBalanceSatoshis = bsvBalance?.Satoshis ?? 0,
            TotalUtxoCount = bsvUtxoCount + tokenUtxoCount,
            BsvUtxoCount = bsvUtxoCount,
            TokenUtxoCount = tokenUtxoCount,
            TransactionCount = transactionCount,
            FirstTransactionAt = first?.Timestamp,
            FirstTransactionBlockHeight = first?.Height,
            LastTransactionAt = last?.Timestamp,
            LastTransactionBlockHeight = last?.Height,
            LastProjectionSequence = Math.Max(bsvBalance?.LastSequence ?? 0L, last?.LastSequence ?? 0L),
            TokenBalances = [.. tokenBalances]
        };
    }

    private async Task<AdminTrackedTokenSummaryResponse> BuildTokenSummaryAsync(
        string tokenId,
        CancellationToken cancellationToken)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();

        var state = await tokenProjectionReader.LoadStateAsync(tokenId, cancellationToken);
        var holderCount = await session.Query<AddressBalanceProjectionDocument>()
            .Where(x => x.TokenId == tokenId)
            .CountAsync(cancellationToken);
        var utxoCount = await session.Query<AddressUtxoProjectionDocument>()
            .Where(x => x.TokenId == tokenId)
            .CountAsync(cancellationToken);

        var historyQuery = session.Query<TokenHistoryProjectionDocument>()
            .Where(x => x.TokenId == tokenId);
        var transactionCount = await historyQuery.CountAsync(cancellationToken);
        var first = await historyQuery
            .OrderBy(x => x.Timestamp)
            .ThenBy(x => x.TxId)
            .Select(x => new ActivitySnapshot
            {
                Timestamp = x.Timestamp,
                Height = x.Height,
                LastSequence = x.LastSequence
            })
            .FirstOrDefaultAsync(cancellationToken);
        var last = await historyQuery
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.TxId)
            .Select(x => new ActivitySnapshot
            {
                Timestamp = x.Timestamp,
                Height = x.Height,
                LastSequence = x.LastSequence
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminTrackedTokenSummaryResponse
        {
            ProtocolType = state?.ProtocolType,
            ValidationStatus = state?.ValidationStatus,
            Issuer = state?.Issuer,
            RedeemAddress = state?.RedeemAddress,
            LocalKnownSupplySatoshis = state?.TotalKnownSupply,
            BurnedSatoshis = state?.BurnedSatoshis,
            HolderCount = holderCount,
            UtxoCount = utxoCount,
            TransactionCount = transactionCount,
            FirstTransactionAt = first?.Timestamp,
            FirstTransactionBlockHeight = first?.Height,
            LastTransactionAt = last?.Timestamp,
            LastTransactionBlockHeight = last?.Height,
            LastProjectionSequence = state?.LastSequence ?? last?.LastSequence
        };
    }

    private static AdminTrackedAddressResponse BuildAddressResponse(
        TrackedAddressDocument tracked,
        TrackedAddressStatusDocument status,
        AdminTrackedAddressSummaryResponse summary = null)
    {
        var source = (TrackedEntityDocumentBase)status ?? tracked;
        return new AdminTrackedAddressResponse
        {
            Address = tracked.Address,
            Name = tracked.Name,
            IsTombstoned = source.IsTombstoned,
            TombstonedAt = source.TombstonedAt,
            CreatedAt = tracked.CreatedAt,
            UpdatedAt = status?.UpdatedAt ?? tracked.UpdatedAt,
            FailureReason = status?.FailureReason,
            IntegritySafe = status?.IntegritySafe,
            Readiness = TrackedEntityReadinessMapper.Map(source),
            Summary = summary
        };
    }

    private static AdminTrackedTokenResponse BuildTokenResponse(
        TrackedTokenDocument tracked,
        TrackedTokenStatusDocument status,
        AdminTrackedTokenSummaryResponse summary = null)
    {
        var source = (TrackedEntityDocumentBase)status ?? tracked;
        return new AdminTrackedTokenResponse
        {
            TokenId = tracked.TokenId,
            Symbol = tracked.Symbol,
            IsTombstoned = source.IsTombstoned,
            TombstonedAt = source.TombstonedAt,
            CreatedAt = tracked.CreatedAt,
            UpdatedAt = status?.UpdatedAt ?? tracked.UpdatedAt,
            FailureReason = status?.FailureReason,
            IntegritySafe = status?.IntegritySafe,
            Readiness = TrackedEntityReadinessMapper.Map(source),
            Summary = summary
        };
    }

    private sealed class ActivitySnapshot
    {
        public long? Timestamp { get; set; }
        public int? Height { get; set; }
        public long? LastSequence { get; set; }
    }
}
