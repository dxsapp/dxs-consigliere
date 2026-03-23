using Dxs.Bsv;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Services.Impl;

public class TransactionQueryService(IDocumentStore store) : ITransactionQueryService
{
    private const int MaxBatchCount = 1000;
    private const int MaxBlockPageSize = 500;

    public async Task<string> GetTransactionAsync(string id, CancellationToken cancellationToken = default)
    {
        ValidateTransactionId(id);

        using var session = store.GetNoCacheNoTrackingSession();
        var transaction = await session.LoadAsync<TransactionHexData>(
            TransactionHexData.GetId(id),
            cancellationToken
        );

        if (transaction == null)
            throw new TransactionQueryException(TransactionQueryErrorKind.NotFound, "Not found");

        if (transaction.Hex?.Length > 0)
            return transaction.Hex;

        throw new TransactionQueryException(
            TransactionQueryErrorKind.InternalError,
            "Missing transaction data"
        );
    }

    public async Task<Dictionary<string, string>> GetTransactionsAsync(
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default
    )
    {
        if (ids == null)
            throw new TransactionQueryException(
                TransactionQueryErrorKind.BadRequest,
                "Ids not specified"
            );

        if (ids.Count > MaxBatchCount)
            throw new TransactionQueryException(
                TransactionQueryErrorKind.BadRequest,
                $"Too much transactions ids in request, max {MaxBatchCount}"
            );

        var checkedIds = ids.Select(ValidateTransactionId).ToList();

        using var session = store.GetNoCacheNoTrackingSession();
        var transactions = await session.LoadAsync<TransactionHexData>(
            checkedIds.Select(TransactionHexData.GetId),
            cancellationToken
        );

        return transactions.ToDictionary(
            x => TransactionHexData.Parse(x.Key),
            x => x.Value is { } mtx
                ? mtx.Hex
                : string.Empty
        );
    }

    public async Task<GetTransactionsByBlockResponse> GetTransactionsByBlockAsync(
        int blockHeight,
        int skip,
        CancellationToken cancellationToken = default
    )
    {
        if (blockHeight == 0)
            throw new TransactionQueryException(
                TransactionQueryErrorKind.BadRequest,
                "Block height not specified"
            );

        using var session = store.GetNoCacheNoTrackingSession();

        var txIds = await session.Query<MetaTransaction>()
            .Where(x => x.Height == blockHeight)
            .OrderBy(x => x.Index)
            .Skip(skip)
            .Take(MaxBlockPageSize)
            .Select(x => x.Id)
            .ToListAsync(token: cancellationToken);

        var result = new GetTransactionsByBlockResponse
        {
            BlockHeight = blockHeight,
            PageSize = MaxBlockPageSize,
        };

        var datasIds = txIds.Select(TransactionHexData.GetId).ToList();
        IQueryable<TransactionHexData> datasQuery = session
            .Query<TransactionHexData>()
            .Where(x => x.TxId.In(datasIds));

        await foreach (var (data, totalCount) in session
            .Enumerate(datasQuery)
            .WithCancellation(cancellationToken))
        {
            result.TotalCount = totalCount;
            result.Transactions.Add(data.TxId, data.Hex);
        }

        return result;
    }

    public async Task<ValidateStasResponse> ValidateStasTransactionAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        ValidateTransactionId(id);

        using var session = store.GetNoCacheNoTrackingSession();
        var metaTransaction = await session.LoadAsync<MetaTransaction>(id, cancellationToken);

        if (metaTransaction == null)
            throw new TransactionQueryException(TransactionQueryErrorKind.NotFound, "Not found");

        var askLater = !metaTransaction.IsIssue && !metaTransaction.AllStasInputsKnown;

        if (!askLater && !metaTransaction.IsStas)
        {
            throw new TransactionQueryException(
                TransactionQueryErrorKind.NotStas,
                "This is not a STAS transaction"
            );
        }

        return new ValidateStasResponse(
            askLater,
            metaTransaction.Id,
            metaTransaction.IllegalRoots.Count == 0,
            metaTransaction.IsIssue,
            metaTransaction.IsRedeem,
            metaTransaction.DstasEventType,
            metaTransaction.DstasSpendingType,
            metaTransaction.DstasOptionalDataContinuity,
            metaTransaction.TokenIds.First(),
            [],
            metaTransaction.IllegalRoots.ToArray()
        );
    }

    private static string ValidateTransactionId(string id)
    {
        if (id.Length != 64 || id.Any(x => !HexConverter.IsHexChar(x)))
        {
            throw new TransactionQueryException(
                TransactionQueryErrorKind.BadRequest,
                $"Malformed transaction id: \"{id}\""
            );
        }

        return id;
    }
}
