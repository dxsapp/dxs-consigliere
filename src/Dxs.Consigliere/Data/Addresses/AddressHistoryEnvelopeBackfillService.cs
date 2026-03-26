#nullable enable

using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Journal;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Data.Addresses;

public interface IAddressHistoryEnvelopeBackfillService
{
    Task<AddressHistoryEnvelopeBackfillBatchResult> BackfillBatchAsync(int take, CancellationToken cancellationToken = default);
    Task<long> CountPendingAsync(CancellationToken cancellationToken = default);
}

public interface IAddressHistoryEnvelopeBackfillTelemetry
{
    AddressHistoryEnvelopeBackfillTelemetrySnapshot GetSnapshot();
}

public sealed class AddressHistoryEnvelopeBackfillService(
    IDocumentStore documentStore
) : IAddressHistoryEnvelopeBackfillService, IAddressHistoryEnvelopeBackfillTelemetry
{
    private long _lastStartedAtTicks;
    private long _lastCompletedAtTicks;
    private long _lastBatchScanned;
    private long _lastBatchRewritten;
    private long _lastBatchMissingTransactions;
    private long _lastKnownPendingCount;
    private long _lastTouchedSequence;

    private const int DefaultTake = 128;

    public async Task<AddressHistoryEnvelopeBackfillBatchResult> BackfillBatchAsync(
        int take,
        CancellationToken cancellationToken = default
    )
    {
        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take));

        Interlocked.Exchange(ref _lastStartedAtTicks, DateTimeOffset.UtcNow.UtcTicks);

        using var session = documentStore.GetSession();
        var applications = await QueryPending(session)
            .Take(take)
            .ToListAsync(token: cancellationToken);

        if (applications.Count == 0)
        {
            var pending = await CountPendingAsync(cancellationToken);
            var empty = new AddressHistoryEnvelopeBackfillBatchResult(0, 0, 0, pending, 0);
            Record(empty);
            return empty;
        }

        var transactions = await session.LoadAsync<MetaTransaction>(
            applications.Select(x => x.TxId).Distinct(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
        var rewritten = 0;
        var missingTransactions = 0;
        long lastTouchedSequence = 0;

        foreach (var application in applications)
        {
            if (!transactions.TryGetValue(application.TxId, out var transaction) || transaction is null)
            {
                missingTransactions++;
                continue;
            }

            AddressHistoryEnvelopeHelper.Hydrate(
                application,
                transaction,
                application.Debits ?? [],
                application.Credits ?? []);
            rewritten++;
            lastTouchedSequence = Math.Max(lastTouchedSequence, application.LastSequence);
            await session.StoreAsync(application, application.Id, cancellationToken);
        }

        if (rewritten > 0)
            await session.SaveChangesAsync(cancellationToken);

        var remainingPending = await CountPendingAsync(cancellationToken);
        var result = new AddressHistoryEnvelopeBackfillBatchResult(
            applications.Count,
            rewritten,
            missingTransactions,
            remainingPending,
            lastTouchedSequence);
        Record(result);
        return result;
    }

    public async Task<long> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        return await QueryPending(session).LongCountAsync(token: cancellationToken);
    }

    public AddressHistoryEnvelopeBackfillTelemetrySnapshot GetSnapshot()
        => new(
            Volatile.Read(ref _lastBatchScanned),
            Volatile.Read(ref _lastBatchRewritten),
            Volatile.Read(ref _lastBatchMissingTransactions),
            Volatile.Read(ref _lastKnownPendingCount),
            Volatile.Read(ref _lastTouchedSequence),
            ReadTimestamp(Volatile.Read(ref _lastStartedAtTicks)),
            ReadTimestamp(Volatile.Read(ref _lastCompletedAtTicks)));

    private void Record(AddressHistoryEnvelopeBackfillBatchResult result)
    {
        Interlocked.Exchange(ref _lastBatchScanned, result.Scanned);
        Interlocked.Exchange(ref _lastBatchRewritten, result.Rewritten);
        Interlocked.Exchange(ref _lastBatchMissingTransactions, result.MissingTransactions);
        Interlocked.Exchange(ref _lastKnownPendingCount, result.PendingCount);
        Interlocked.Exchange(ref _lastTouchedSequence, result.LastTouchedSequence);
        Interlocked.Exchange(ref _lastCompletedAtTicks, DateTimeOffset.UtcNow.UtcTicks);
    }

    private static IQueryable<AddressProjectionAppliedTransactionDocument> QueryPending(IAsyncDocumentSession session)
        => session.Query<AddressProjectionAppliedTransactionDocument>()
            .Where(x =>
                x.Timestamp == null
                || x.Height == null
                || x.ValidStasTx == null
                || x.TxFeeSatoshis == null
                || x.Note == null
                || x.FromAddresses == null
                || x.ToAddresses == null)
            .OrderBy(x => x.LastSequence)
            .ThenBy(x => x.TxId);

    private static DateTimeOffset? ReadTimestamp(long ticks)
        => ticks <= 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
}

public sealed record AddressHistoryEnvelopeBackfillBatchResult(
    int Scanned,
    int Rewritten,
    int MissingTransactions,
    long PendingCount,
    long LastTouchedSequence
);

public sealed record AddressHistoryEnvelopeBackfillTelemetrySnapshot(
    long LastBatchScanned,
    long LastBatchRewritten,
    long LastBatchMissingTransactions,
    long PendingCount,
    long LastTouchedSequence,
    DateTimeOffset? LastRunStartedAt,
    DateTimeOffset? LastRunCompletedAt
);
