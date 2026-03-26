#nullable enable

using Dxs.Bsv.Script;
using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.History;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.Addresses;

public sealed class AddressHistoryProjectionReader(
    IDocumentStore documentStore,
    INetworkProvider networkProvider,
    IProjectionReadCache projectionReadCache,
    IProjectionReadCacheKeyFactory cacheKeyFactory
)
{
    public AddressHistoryProjectionReader(
        IDocumentStore documentStore,
        INetworkProvider networkProvider
    )
        : this(documentStore, networkProvider, new NoopProjectionReadCache(), new ProjectionReadCacheKeyFactory())
    {
    }

    private const int MaxTake = 100;

    public async Task<AddressHistoryResponse> GetHistory(
        GetAddressHistoryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request.Take > MaxTake)
            throw new Exception($"Requested page is too big, max per request: {MaxTake} < {request.Take}");

        var address = request.Address.EnsureValidBsvAddress();
        var tokenSelection = NormalizeTokenSelection(request.TokenIds);
        var normalizedRequest = new GetAddressHistoryRequest(
            address.Value,
            tokenSelection.CandidateTokenIds.Select(x => string.IsNullOrWhiteSpace(x) ? "bsv" : x).ToArray(),
            request.Desc,
            request.SkipZeroBalance,
            request.Skip,
            request.Take <= 0 ? MaxTake : request.Take);
        var descriptor = cacheKeyFactory.CreateAddressHistory(normalizedRequest, address.Value, tokenSelection.CandidateTokenIds);

        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            new ProjectionCacheEntryOptions { Tags = descriptor.Tags },
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                if (tokenSelection.IsEmpty)
                {
                    return new AddressHistoryResponse
                    {
                        History = [],
                        TotalCount = 0
                    };
                }

                var optimized = await TryGetHistoryFromEnvelopeAsync(
                    session,
                    address.Value,
                    request,
                    tokenSelection,
                    ct);
                if (optimized is not null)
                    return optimized;

                return await GetHistoryLegacyAsync(
                    session,
                    address.Value,
                    request,
                    tokenSelection,
                    ct);
            },
            cancellationToken);
    }

    private async Task<AddressHistoryResponse?> TryGetHistoryFromEnvelopeAsync(
        Raven.Client.Documents.Session.IAsyncDocumentSession session,
        string address,
        GetAddressHistoryRequest request,
        TokenSelection tokenSelection,
        CancellationToken cancellationToken
    )
    {
        var effectiveTake = request.Take <= 0 ? MaxTake : request.Take;
        var batchSize = Math.Clamp((request.Skip + effectiveTake) * 2, 64, 512);
        var applicationSkip = 0;
        var pageRows = new List<AddressHistory>(effectiveTake);
        var skippedRows = 0;
        var totalCount = 0;

        while (true)
        {
            var batch = await BuildApplicationQuery(session, address, request.Desc)
                .Skip(applicationSkip)
                .Take(batchSize)
                .ToListAsync(token: cancellationToken);

            if (batch.Count == 0)
                break;

            if (batch.Any(x => !AddressHistoryEnvelopeHelper.HasHistoryEnvelope(x)))
                return null;

            foreach (var application in batch)
            {
                var applicationRows = CreateRowsFromApplication(
                    address,
                    request.SkipZeroBalance,
                    tokenSelection,
                    application);
                if (applicationRows.Count == 0)
                    continue;

                totalCount += applicationRows.Count;

                foreach (var row in applicationRows)
                {
                    if (skippedRows < request.Skip)
                    {
                        skippedRows++;
                        continue;
                    }

                    if (pageRows.Count < effectiveTake)
                        pageRows.Add(row);
                }
            }

            applicationSkip += batch.Count;
        }

        return new AddressHistoryResponse
        {
            History = pageRows.Select(AddressHistoryDto.From).ToArray(),
            TotalCount = totalCount
        };
    }

    private async Task<AddressHistoryResponse> GetHistoryLegacyAsync(
        Raven.Client.Documents.Session.IAsyncDocumentSession session,
        string address,
        GetAddressHistoryRequest request,
        TokenSelection tokenSelection,
        CancellationToken cancellationToken
    )
    {
        var applications = await BuildApplicationQuery(session, address, request.Desc)
            .ToListAsync(token: cancellationToken);

        if (applications.Count == 0)
        {
            return new AddressHistoryResponse
            {
                History = [],
                TotalCount = 0
            };
        }

        var txIds = applications
            .Select(x => x.TxId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var transactions = await session.LoadAsync<MetaTransaction>(txIds, cancellationToken);
        var transactionLookup = transactions.Values
            .Where(x => x is not null)
            .ToDictionary(x => x!.Id, x => x!, StringComparer.OrdinalIgnoreCase);

        var inputIds = transactionLookup.Values
            .SelectMany(x => x.Inputs ?? [])
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var inputOutputs = inputIds.Length == 0
            ? new Dictionary<string, MetaOutput>(StringComparer.OrdinalIgnoreCase)
            : (await session.LoadAsync<MetaOutput>(inputIds, cancellationToken))
                .Values
                .Where(x => x is not null)
                .ToDictionary(x => x!.Id, x => x!, StringComparer.OrdinalIgnoreCase);

        var rows = new List<AddressHistory>();
        foreach (var application in applications)
        {
            if (!transactionLookup.TryGetValue(application.TxId, out var transaction))
                continue;

            if (!TryBuildRows(
                    address,
                    request.SkipZeroBalance,
                    tokenSelection,
                    application,
                    transaction,
                    inputOutputs,
                    rows))
            {
                continue;
            }
        }

        var ordered = request.Desc
            ? rows.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.TxId, StringComparer.OrdinalIgnoreCase)
            : rows.OrderBy(x => x.Timestamp).ThenBy(x => x.TxId, StringComparer.OrdinalIgnoreCase);

        var paged = ordered
            .Skip(request.Skip)
            .Take(request.Take <= 0 ? MaxTake : request.Take)
            .ToArray();

        return new AddressHistoryResponse
        {
            History = paged.Select(AddressHistoryDto.From).ToArray(),
            TotalCount = rows.Count
        };
    }

    private bool TryBuildRows(
        string address,
        bool skipZeroBalance,
        TokenSelection tokenSelection,
        AddressProjectionAppliedTransactionDocument application,
        MetaTransaction transaction,
        IReadOnlyDictionary<string, MetaOutput> inputOutputs,
        List<AddressHistory> rows
    )
    {
        if (transaction.Inputs?.Count > 0 && inputOutputs.Count < transaction.Inputs.Count)
            return false;

        var totalSpentSatoshis = transaction.Inputs?
            .Select(x => inputOutputs.TryGetValue(x.Id, out var output) ? output.Satoshis : 0)
            .Sum() ?? 0;
        var totalReceivedSatoshis = (transaction.Outputs ?? [])
            .Where(ShouldProjectOutput)
            .Sum(x => x.Satoshis);
        var totalFeeSatoshis = totalSpentSatoshis - totalReceivedSatoshis;

        var fromAddresses = transaction.Inputs?
            .Select(x => inputOutputs.TryGetValue(x.Id, out var output) ? output.Address : null)
            .Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, address, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var toAddresses = (transaction.Outputs ?? [])
            .Where(ShouldProjectOutput)
            .Select(x => x.Address)
            .Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, address, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tokenId in tokenSelection.CandidateTokenIds)
        {
            var spent = application.Debits
                .Where(x => MatchesToken(x.TokenId, tokenId) && string.Equals(x.Address, address, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Satoshis);

            var received = application.Credits
                .Where(x => MatchesToken(x.TokenId, tokenId) && string.Equals(x.Address, address, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Satoshis);

            if (spent == 0 && received == 0)
                continue;

            var history = new AddressHistory
            {
                Address = address,
                TokenId = tokenId,
                TxId = transaction.Id,
                ScriptType = GetScriptType(application, tokenId),
                Timestamp = transaction.Timestamp,
                Height = transaction.Height,
                ValidStasTx = transaction.IsIssue
                    ? transaction.IsValidIssue
                    : !transaction.IllegalRoots.Any(),
                SpentSatoshis = spent,
                ReceivedSatoshis = received,
                BalanceSatoshis = received - spent,
                TxFeeSatoshis = totalFeeSatoshis,
                Note = transaction.Note,
                Side = 0,
                FromAddresses = fromAddresses,
                ToAddresses = toAddresses
            };

            if (skipZeroBalance && history.BalanceSatoshis == 0)
                continue;

            rows.Add(history);
        }

        return true;
    }

    private static List<AddressHistory> CreateRowsFromApplication(
        string address,
        bool skipZeroBalance,
        TokenSelection tokenSelection,
        AddressProjectionAppliedTransactionDocument application
    )
    {
        var rows = new List<AddressHistory>();
        var fromAddresses = application.FromAddresses?
            .Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, address, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var toAddresses = application.ToAddresses?
            .Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, address, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        foreach (var tokenId in tokenSelection.CandidateTokenIds)
        {
            var spent = application.Debits
                .Where(x => MatchesToken(x.TokenId, tokenId) && string.Equals(x.Address, address, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Satoshis);

            var received = application.Credits
                .Where(x => MatchesToken(x.TokenId, tokenId) && string.Equals(x.Address, address, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Satoshis);

            if (spent == 0 && received == 0)
                continue;

            var history = new AddressHistory
            {
                Address = address,
                TokenId = tokenId,
                TxId = application.TxId,
                ScriptType = GetScriptType(application, tokenId),
                Timestamp = application.Timestamp ?? 0,
                Height = application.Height ?? MetaTransaction.DefaultHeight,
                ValidStasTx = application.ValidStasTx ?? false,
                SpentSatoshis = spent,
                ReceivedSatoshis = received,
                BalanceSatoshis = received - spent,
                TxFeeSatoshis = application.TxFeeSatoshis ?? 0,
                Note = application.Note,
                Side = 0,
                FromAddresses = fromAddresses,
                ToAddresses = toAddresses
            };

            if (skipZeroBalance && history.BalanceSatoshis == 0)
                continue;

            rows.Add(history);
        }

        return rows;
    }

    private TokenSelection NormalizeTokenSelection(string[]? tokenIds)
    {
        if (tokenIds is null)
            return new TokenSelection([null]);

        var tokenSelection = new HashSet<string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var tokenId in tokenIds)
        {
            if (string.IsNullOrWhiteSpace(tokenId))
                continue;

            if (tokenId.Equals("bsv", StringComparison.InvariantCultureIgnoreCase))
            {
                tokenSelection.Add(null);
                continue;
            }

            tokenSelection.Add(tokenId.EnsureValidTokenId(networkProvider.Network).Value);
        }

        return new TokenSelection(tokenSelection.ToArray());
    }

    private static ScriptType GetScriptType(AddressProjectionAppliedTransactionDocument application, string? tokenId)
    {
        var debit = application.Debits.FirstOrDefault(x => MatchesToken(x.TokenId, tokenId));
        if (debit is not null)
            return debit.ScriptType;

        var credit = application.Credits.FirstOrDefault(x => MatchesToken(x.TokenId, tokenId));
        if (credit is not null)
            return credit.ScriptType;

        return ScriptType.P2PKH;
    }

    private static bool MatchesToken(string? actualTokenId, string? requestedTokenId)
        => string.IsNullOrWhiteSpace(requestedTokenId)
            ? string.IsNullOrWhiteSpace(actualTokenId)
            : string.Equals(actualTokenId, requestedTokenId, StringComparison.OrdinalIgnoreCase);

    private static bool ShouldProjectOutput(MetaTransaction.Output output)
        => output.Type is ScriptType.P2PKH or ScriptType.P2MPKH or ScriptType.P2STAS or ScriptType.DSTAS;

    private static IQueryable<AddressProjectionAppliedTransactionDocument> BuildApplicationQuery(
        Raven.Client.Documents.Session.IAsyncDocumentSession session,
        string address,
        bool desc
    )
    {
        var query = session.Query<AddressProjectionAppliedTransactionDocument>()
            .Where(x =>
                x.Credits.Any(y => y.Address == address)
                || x.Debits.Any(y => y.Address == address));

        return desc
            ? query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.TxId)
            : query.OrderBy(x => x.Timestamp).ThenBy(x => x.TxId);
    }

    private readonly record struct TokenSelection(string?[] CandidateTokenIds)
    {
        public bool IsEmpty => CandidateTokenIds.Length == 0;
    }
}
