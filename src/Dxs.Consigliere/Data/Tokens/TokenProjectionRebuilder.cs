using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Data.Tokens;

public sealed class TokenProjectionRebuilder(
    IDocumentStore documentStore,
    RavenObservationJournalReader journalReader,
    AddressProjectionRebuilder addressProjectionRebuilder
)
{
    private const int DefaultPageSize = 512;

    public async Task<ProjectionCheckpoint> RebuildAsync(
        int take = DefaultPageSize,
        CancellationToken cancellationToken = default
    )
    {
        await addressProjectionRebuilder.RebuildAsync(cancellationToken: cancellationToken);

        var checkpoint = await LoadCheckpointAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var records = await journalReader.ReadAsync(checkpoint.Sequence, take, cancellationToken);
            if (records.Count == 0)
                return checkpoint;

            using var session = documentStore.GetSession();
            var batchContext = await LoadBatchContextAsync(session, records, cancellationToken);

            foreach (var record in records)
            {
                var applied = await ApplyAsync(session, batchContext, record, cancellationToken);
                if (!applied)
                {
                    await StoreCheckpointAsync(session, checkpoint, cancellationToken);
                    await session.SaveChangesAsync(cancellationToken);
                    return checkpoint;
                }

                checkpoint = checkpoint.AdvanceTo(record.Sequence, record.Fingerprint);
            }

            await RecomputeTouchedTokenStatesAsync(session, batchContext.DirtyTokenIds, checkpoint.Sequence.Value, cancellationToken);
            await StoreCheckpointAsync(session, checkpoint, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        return checkpoint;
    }

    private async Task<bool> ApplyAsync(
        IAsyncDocumentSession session,
        TokenProjectionBatchContext batchContext,
        StoredObservationJournalRecord record,
        CancellationToken cancellationToken
    )
    {
        if (record.IsObservationType<TxObservation>())
            return await ApplyTxObservationAsync(session, batchContext, record, record.Deserialize<TxObservation>(), cancellationToken);

        if (record.IsObservationType<BlockObservation>())
        {
            await ApplyBlockObservationAsync(session, batchContext, record, record.Deserialize<BlockObservation>(), cancellationToken);
            return true;
        }

        return true;
    }

    private static async Task<bool> ApplyTxObservationAsync(
        IAsyncDocumentSession session,
        TokenProjectionBatchContext batchContext,
        StoredObservationJournalRecord record,
        TxObservation observation,
        CancellationToken cancellationToken
    )
    {
        if (!batchContext.Applications.TryGetValue(observation.TxId, out var application))
        {
            application = new TokenProjectionAppliedTransactionDocument
            {
                Id = TokenProjectionAppliedTransactionDocument.GetId(observation.TxId),
                TxId = observation.TxId
            };

            batchContext.Applications[observation.TxId] = application;
        }

        switch (observation.EventType)
        {
            case TxObservationEventType.SeenInMempool:
                if (string.Equals(application.AppliedState, TokenProjectionApplicationState.Confirmed, StringComparison.Ordinal)
                    || string.Equals(application.AppliedState, TokenProjectionApplicationState.Pending, StringComparison.Ordinal))
                {
                    await UpdateExistingStateAsync(session, application, observation, record, cancellationToken);
                    return true;
                }

                return await TryApplyTransactionAsync(
                    session,
                    batchContext,
                    application,
                    observation,
                    record,
                    TokenProjectionApplicationState.Pending,
                    cancellationToken);

            case TxObservationEventType.SeenInBlock:
                if (string.Equals(application.AppliedState, TokenProjectionApplicationState.Pending, StringComparison.Ordinal)
                    || string.Equals(application.AppliedState, TokenProjectionApplicationState.Confirmed, StringComparison.Ordinal))
                {
                    application.AppliedState = TokenProjectionApplicationState.Confirmed;
                    application.ConfirmedBlockHash = observation.BlockHash;
                    Touch(application, observation, record);
                    await UpdateHistoryBlockHashAsync(session, application.HistoryDocumentIds, observation.BlockHash, record.Sequence.Value, cancellationToken);
                    await session.StoreAsync(application, application.Id, cancellationToken);
                    batchContext.DirtyTokenIds.UnionWith(application.TokenIds ?? []);
                    return true;
                }

                return await TryApplyTransactionAsync(
                    session,
                    batchContext,
                    application,
                    observation,
                    record,
                    TokenProjectionApplicationState.Confirmed,
                    cancellationToken);

            case TxObservationEventType.DroppedBySource:
                if (!string.Equals(application.AppliedState, TokenProjectionApplicationState.Pending, StringComparison.Ordinal))
                {
                    Touch(application, observation, record);
                    await session.StoreAsync(application, application.Id, cancellationToken);
                    return true;
                }

                await RemoveHistoryAsync(session, application.HistoryDocumentIds, cancellationToken);
                batchContext.DirtyTokenIds.UnionWith(application.TokenIds ?? []);
                application.AppliedState = TokenProjectionApplicationState.None;
                application.ConfirmedBlockHash = null;
                application.HistoryDocumentIds = [];
                Touch(application, observation, record);
                await session.StoreAsync(application, application.Id, cancellationToken);
                return true;
        }

        return true;
    }

    private static async Task<bool> TryApplyTransactionAsync(
        IAsyncDocumentSession session,
        TokenProjectionBatchContext batchContext,
        TokenProjectionAppliedTransactionDocument application,
        TxObservation observation,
        StoredObservationJournalRecord record,
        string targetState,
        CancellationToken cancellationToken
    )
    {
        if (!batchContext.MetaTransactions.TryGetValue(observation.TxId, out var transaction))
            return false;

        var tokenIds = (transaction.TokenIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tokenIds.Length == 0)
        {
            application.AppliedState = targetState;
            application.TokenIds = [];
            application.HistoryDocumentIds = [];
            application.ConfirmedBlockHash = targetState == TokenProjectionApplicationState.Confirmed ? observation.BlockHash : null;
            Touch(application, observation, record);
            await session.StoreAsync(application, application.Id, cancellationToken);
            return true;
        }

        var historyDocuments = tokenIds
            .Select(tokenId => CreateHistoryDocument(transaction, tokenId, batchContext.InputOutputs, observation, record.Sequence.Value))
            .ToArray();

        foreach (var history in historyDocuments)
            await session.StoreAsync(history, history.Id, cancellationToken);

        application.AppliedState = targetState;
        application.ConfirmedBlockHash = targetState == TokenProjectionApplicationState.Confirmed ? observation.BlockHash : null;
        application.TokenIds = tokenIds;
        application.HistoryDocumentIds = historyDocuments.Select(x => x.Id).ToArray();
        Touch(application, observation, record);
        await session.StoreAsync(application, application.Id, cancellationToken);

        batchContext.DirtyTokenIds.UnionWith(tokenIds);
        return true;
    }

    private static async Task ApplyBlockObservationAsync(
        IAsyncDocumentSession session,
        TokenProjectionBatchContext batchContext,
        StoredObservationJournalRecord record,
        BlockObservation observation,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(observation.EventType, BlockObservationEventType.Disconnected, StringComparison.Ordinal))
            return;

        var applications = batchContext.Applications.Values
            .Where(x => string.Equals(x.AppliedState, TokenProjectionApplicationState.Confirmed, StringComparison.Ordinal)
                && string.Equals(x.ConfirmedBlockHash, observation.BlockHash, StringComparison.Ordinal))
            .ToList();

        if (applications.Count == 0)
        {
            applications = await session.Query<TokenProjectionAppliedTransactionDocument>()
                .Where(x => x.AppliedState == TokenProjectionApplicationState.Confirmed && x.ConfirmedBlockHash == observation.BlockHash)
                .ToListAsync(token: cancellationToken);

            foreach (var application in applications)
                batchContext.Applications[application.TxId] = application;
        }

        foreach (var application in applications)
        {
            await RemoveHistoryAsync(session, application.HistoryDocumentIds, cancellationToken);
            batchContext.DirtyTokenIds.UnionWith(application.TokenIds ?? []);
            application.AppliedState = TokenProjectionApplicationState.None;
            application.ConfirmedBlockHash = null;
            application.HistoryDocumentIds = [];
            application.LastObservedAt = observation.ObservedAt ?? record.AppendedAt;
            application.LastSequence = record.Sequence.Value;
            await session.StoreAsync(application, application.Id, cancellationToken);
        }
    }

    private static async Task UpdateExistingStateAsync(
        IAsyncDocumentSession session,
        TokenProjectionAppliedTransactionDocument application,
        TxObservation observation,
        StoredObservationJournalRecord record,
        CancellationToken cancellationToken
    )
    {
        Touch(application, observation, record);
        if (string.Equals(application.AppliedState, TokenProjectionApplicationState.Confirmed, StringComparison.Ordinal))
            application.ConfirmedBlockHash = observation.BlockHash ?? application.ConfirmedBlockHash;

        await session.StoreAsync(application, application.Id, cancellationToken);
    }

    private static TokenHistoryProjectionDocument CreateHistoryDocument(
        MetaTransaction transaction,
        string tokenId,
        IReadOnlyDictionary<string, MetaOutput> inputOutputs,
        TxObservation observation,
        long sequence
    )
    {
        var received = (transaction.Outputs ?? [])
            .Where(x => string.Equals(x.TokenId, tokenId, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Satoshis);
        var spent = (transaction.Inputs ?? [])
            .Select(x => inputOutputs.TryGetValue(x.Id, out var output) ? output : null)
            .Where(x => x is not null && string.Equals(x.TokenId, tokenId, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x!.Satoshis);

        return new TokenHistoryProjectionDocument
        {
            Id = TokenHistoryProjectionDocument.GetId(tokenId, transaction.Id),
            TokenId = tokenId,
            TxId = transaction.Id,
            Timestamp = transaction.Timestamp,
            Height = transaction.Height,
            ReceivedSatoshis = received,
            SpentSatoshis = spent,
            BalanceDeltaSatoshis = received - spent,
            IsIssue = transaction.IsIssue,
            IsRedeem = transaction.IsRedeem,
            ValidationStatus = GetTransactionValidationStatus(transaction),
            ProtocolType = GetProtocolType(transaction),
            ConfirmedBlockHash = observation.EventType == TxObservationEventType.SeenInBlock ? observation.BlockHash : null,
            LastSequence = sequence
        };
    }

    private static async Task UpdateHistoryBlockHashAsync(
        IAsyncDocumentSession session,
        IEnumerable<string> historyDocumentIds,
        string blockHash,
        long sequence,
        CancellationToken cancellationToken
    )
    {
        foreach (var historyId in historyDocumentIds ?? [])
        {
            var history = await session.LoadAsync<TokenHistoryProjectionDocument>(historyId, cancellationToken);
            if (history is null)
                continue;

            history.ConfirmedBlockHash = blockHash;
            history.LastSequence = sequence;
        }
    }

    private static async Task RemoveHistoryAsync(
        IAsyncDocumentSession session,
        IEnumerable<string> historyDocumentIds,
        CancellationToken cancellationToken
    )
    {
        foreach (var historyId in historyDocumentIds ?? [])
        {
            var history = await session.LoadAsync<TokenHistoryProjectionDocument>(historyId, cancellationToken);
            if (history is not null)
                session.Delete(history);
        }
    }

    private static async Task RecomputeTouchedTokenStatesAsync(
        IAsyncDocumentSession session,
        IEnumerable<string> tokenIds,
        long sequence,
        CancellationToken cancellationToken
    )
    {
        foreach (var tokenId in tokenIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            await RecomputeTokenStateAsync(session, tokenId, sequence, cancellationToken);
    }

    private static async Task RecomputeTokenStateAsync(
        IAsyncDocumentSession session,
        string tokenId,
        long sequence,
        CancellationToken cancellationToken
    )
    {
        var transactions = await session.Query<MetaTransaction>()
            .Where(x => x.TokenIds.Contains(tokenId))
            .ToListAsync(token: cancellationToken);

        var stateId = TokenStateProjectionDocument.GetId(tokenId);
        var state = await session.LoadAsync<TokenStateProjectionDocument>(stateId, cancellationToken);

        if (transactions.Count == 0)
        {
            if (state is not null)
                session.Delete(state);

            return;
        }

        state ??= new TokenStateProjectionDocument
        {
            Id = stateId,
            TokenId = tokenId
        };

        var issuance = transactions
            .Where(x => x.IsIssue)
            .OrderBy(x => x.Height)
            .ThenBy(x => x.Index)
            .FirstOrDefault();
        var anyInvalid = transactions.Any(x => x.IsIssue ? !x.IsValidIssue : (x.IllegalRoots?.Count ?? 0) > 0);
        var anyUnknown = transactions.Any(x => x.IsStas && !x.IsIssue && (!x.AllStasInputsKnown || (x.MissingTransactions?.Count ?? 0) > 0));
        var utxos = await session.Query<AddressUtxoProjectionDocument>()
            .Where(x => x.TokenId == tokenId)
            .ToListAsync(token: cancellationToken);
        var redeemAddress = issuance?.RedeemAddress;
        var burned = !string.IsNullOrWhiteSpace(redeemAddress)
            ? utxos.Where(x => string.Equals(x.Address, redeemAddress, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Satoshis)
            : 0L;
        var supply = !string.IsNullOrWhiteSpace(redeemAddress)
            ? utxos.Where(x => !string.Equals(x.Address, redeemAddress, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Satoshis)
            : utxos.Sum(x => x.Satoshis);

        state.ProtocolType = transactions.Select(GetProtocolType).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        state.ProtocolVersion = null;
        state.IssuanceKnown = issuance is not null;
        state.ValidationStatus = anyInvalid
            ? TokenProjectionValidationStatus.Invalid
            : issuance is null || anyUnknown
                ? TokenProjectionValidationStatus.Unknown
                : TokenProjectionValidationStatus.Valid;
        state.Issuer = issuance?.RedeemAddress;
        state.RedeemAddress = redeemAddress;
        state.TotalKnownSupply = supply;
        state.BurnedSatoshis = burned;
        state.LastIndexedHeight = transactions.Where(x => x.Height != MetaTransaction.DefaultHeight).Select(x => (int?)x.Height).Max();
        state.LastSequence = sequence;

        await session.StoreAsync(state, state.Id, cancellationToken);
    }

    private static async Task<TokenProjectionBatchContext> LoadBatchContextAsync(
        IAsyncDocumentSession session,
        IReadOnlyList<StoredObservationJournalRecord> records,
        CancellationToken cancellationToken
    )
    {
        var txIds = records
            .Where(x => x.IsObservationType<TxObservation>())
            .Select(x => x.Deserialize<TxObservation>().TxId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var applications = new Dictionary<string, TokenProjectionAppliedTransactionDocument>(StringComparer.OrdinalIgnoreCase);
        if (txIds.Length > 0)
        {
            var loaded = await session.LoadAsync<TokenProjectionAppliedTransactionDocument>(
                txIds.Select(TokenProjectionAppliedTransactionDocument.GetId),
                cancellationToken);

            foreach (var application in loaded.Values.Where(x => x is not null))
                applications[application!.TxId] = application;
        }

        var metaTransactions = new Dictionary<string, MetaTransaction>(StringComparer.OrdinalIgnoreCase);
        if (txIds.Length > 0)
        {
            var loaded = await session.LoadAsync<MetaTransaction>(txIds, cancellationToken);
            foreach (var transaction in loaded.Values.Where(x => x is not null))
                metaTransactions[transaction!.Id] = transaction;
        }

        var inputIds = metaTransactions.Values
            .SelectMany(x => x.Inputs ?? [])
            .Select(x => x.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var inputOutputs = new Dictionary<string, MetaOutput>(StringComparer.OrdinalIgnoreCase);
        if (inputIds.Length > 0)
        {
            var loaded = await session.LoadAsync<MetaOutput>(inputIds, cancellationToken);
            foreach (var output in loaded)
            {
                if (output.Value is not null)
                    inputOutputs[output.Key] = output.Value;
            }
        }

        return new TokenProjectionBatchContext(
            applications,
            metaTransactions,
            inputOutputs,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static string GetProtocolType(MetaTransaction transaction)
    {
        if ((transaction.Outputs ?? []).Any(x => x.Type == ScriptType.DSTAS) ||
            transaction.DstasSpendingType is not null ||
            !string.IsNullOrWhiteSpace(transaction.DstasEventType))
            return TokenProjectionProtocolType.Dstas;

        return transaction.IsStas || (transaction.Outputs ?? []).Any(x => x.Type == ScriptType.P2STAS)
            ? TokenProjectionProtocolType.Stas
            : null;
    }

    private static string GetTransactionValidationStatus(MetaTransaction transaction)
    {
        if (transaction.IsIssue)
            return transaction.IsValidIssue ? TokenProjectionValidationStatus.Valid : TokenProjectionValidationStatus.Invalid;

        if ((transaction.IllegalRoots?.Count ?? 0) > 0)
            return TokenProjectionValidationStatus.Invalid;

        return transaction.AllStasInputsKnown && (transaction.MissingTransactions?.Count ?? 0) == 0
            ? TokenProjectionValidationStatus.Valid
            : TokenProjectionValidationStatus.Unknown;
    }

    private static void Touch(TokenProjectionAppliedTransactionDocument application, TxObservation observation, StoredObservationJournalRecord record)
    {
        application.LastObservedAt = observation.ObservedAt ?? record.AppendedAt;
        application.LastSequence = record.Sequence.Value;
    }

    private async Task<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken cancellationToken)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var checkpoint = await session.LoadAsync<TokenProjectionCheckpointDocument>(TokenProjectionCheckpointDocument.DocumentId, cancellationToken);

        return checkpoint is null
            ? new ProjectionCheckpoint(JournalSequence.Empty)
            : new ProjectionCheckpoint(
                new JournalSequence(checkpoint.LastSequence),
                string.IsNullOrWhiteSpace(checkpoint.LastFingerprint) ? null : new DedupeFingerprint(checkpoint.LastFingerprint)
            );
    }

    private static Task StoreCheckpointAsync(IAsyncDocumentSession session, ProjectionCheckpoint checkpoint, CancellationToken cancellationToken)
        => session.StoreAsync(
            new TokenProjectionCheckpointDocument
            {
                Id = TokenProjectionCheckpointDocument.DocumentId,
                LastSequence = checkpoint.Sequence.Value,
                LastFingerprint = checkpoint.LastAppliedFingerprint?.Value
            },
            TokenProjectionCheckpointDocument.DocumentId,
            cancellationToken
        );

    private sealed record TokenProjectionBatchContext(
        Dictionary<string, TokenProjectionAppliedTransactionDocument> Applications,
        Dictionary<string, MetaTransaction> MetaTransactions,
        Dictionary<string, MetaOutput> InputOutputs,
        HashSet<string> DirtyTokenIds
    );
}
