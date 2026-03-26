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

            foreach (var record in records)
            {
                var applied = await ApplyAsync(session, record, cancellationToken);
                if (!applied)
                {
                    await StoreCheckpointAsync(session, checkpoint, cancellationToken);
                    await session.SaveChangesAsync(cancellationToken);
                    return checkpoint;
                }

                checkpoint = checkpoint.AdvanceTo(record.Sequence, record.Fingerprint);
            }

            await StoreCheckpointAsync(session, checkpoint, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        return checkpoint;
    }

    private async Task<bool> ApplyAsync(
        IAsyncDocumentSession session,
        StoredObservationJournalRecord record,
        CancellationToken cancellationToken
    )
    {
        if (record.IsObservationType<TxObservation>())
            return await ApplyTxObservationAsync(session, record, record.Deserialize<TxObservation>(), cancellationToken);

        if (record.IsObservationType<BlockObservation>())
        {
            await ApplyBlockObservationAsync(session, record, record.Deserialize<BlockObservation>(), cancellationToken);
            return true;
        }

        return true;
    }

    private static async Task<bool> ApplyTxObservationAsync(
        IAsyncDocumentSession session,
        StoredObservationJournalRecord record,
        TxObservation observation,
        CancellationToken cancellationToken
    )
    {
        var application = await session.LoadAsync<TokenProjectionAppliedTransactionDocument>(
                TokenProjectionAppliedTransactionDocument.GetId(observation.TxId),
                cancellationToken)
            ?? new TokenProjectionAppliedTransactionDocument
            {
                Id = TokenProjectionAppliedTransactionDocument.GetId(observation.TxId),
                TxId = observation.TxId
            };

        switch (observation.EventType)
        {
            case TxObservationEventType.SeenInMempool:
                if (string.Equals(application.AppliedState, TokenProjectionApplicationState.Confirmed, StringComparison.Ordinal)
                    || string.Equals(application.AppliedState, TokenProjectionApplicationState.Pending, StringComparison.Ordinal))
                {
                    await UpdateExistingStateAsync(session, application, observation, record, cancellationToken);
                    return true;
                }

                return await TryApplyTransactionAsync(session, application, observation, record, TokenProjectionApplicationState.Pending, cancellationToken);

            case TxObservationEventType.SeenInBlock:
                if (string.Equals(application.AppliedState, TokenProjectionApplicationState.Pending, StringComparison.Ordinal)
                    || string.Equals(application.AppliedState, TokenProjectionApplicationState.Confirmed, StringComparison.Ordinal))
                {
                    application.AppliedState = TokenProjectionApplicationState.Confirmed;
                    application.ConfirmedBlockHash = observation.BlockHash;
                    Touch(application, observation, record);
                    await UpdateHistoryBlockHashAsync(session, application.HistoryDocumentIds, observation.BlockHash, record.Sequence.Value, cancellationToken);
                    await session.StoreAsync(application, application.Id, cancellationToken);
                    return true;
                }

                return await TryApplyTransactionAsync(session, application, observation, record, TokenProjectionApplicationState.Confirmed, cancellationToken);

            case TxObservationEventType.DroppedBySource:
                if (!string.Equals(application.AppliedState, TokenProjectionApplicationState.Pending, StringComparison.Ordinal))
                {
                    Touch(application, observation, record);
                    await session.StoreAsync(application, application.Id, cancellationToken);
                    return true;
                }

                await RemoveHistoryAsync(session, application.HistoryDocumentIds, cancellationToken);
                await RecomputeTouchedTokenStatesAsync(session, application.TokenIds, record.Sequence.Value, cancellationToken);
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
        TokenProjectionAppliedTransactionDocument application,
        TxObservation observation,
        StoredObservationJournalRecord record,
        string targetState,
        CancellationToken cancellationToken
    )
    {
        var transaction = await session.LoadAsync<MetaTransaction>(observation.TxId, cancellationToken);
        if (transaction is null)
            return false;

        var tokenIds = (transaction.TokenIds ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
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

        var inputs = (transaction.Inputs ?? []).Select(x => x.Id).ToArray();
        var inputOutputs = inputs.Length == 0
            ? new Dictionary<string, MetaOutput>()
            : (await session.LoadAsync<MetaOutput>(inputs, cancellationToken))
                .Where(x => x.Value is not null)
                .ToDictionary(x => x.Key, x => x.Value!, StringComparer.OrdinalIgnoreCase);

        var historyDocuments = tokenIds
            .Select(tokenId => CreateHistoryDocument(transaction, tokenId, inputOutputs, observation, record.Sequence.Value))
            .ToArray();

        foreach (var history in historyDocuments)
            await session.StoreAsync(history, history.Id, cancellationToken);

        application.AppliedState = targetState;
        application.ConfirmedBlockHash = targetState == TokenProjectionApplicationState.Confirmed ? observation.BlockHash : null;
        application.TokenIds = tokenIds;
        application.HistoryDocumentIds = historyDocuments.Select(x => x.Id).ToArray();
        Touch(application, observation, record);
        await session.StoreAsync(application, application.Id, cancellationToken);

        await RecomputeTouchedTokenStatesAsync(session, tokenIds, record.Sequence.Value, cancellationToken);
        return true;
    }

    private static async Task ApplyBlockObservationAsync(
        IAsyncDocumentSession session,
        StoredObservationJournalRecord record,
        BlockObservation observation,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(observation.EventType, BlockObservationEventType.Disconnected, StringComparison.Ordinal))
            return;

        var applications = await session.Query<TokenProjectionAppliedTransactionDocument>()
            .Where(x => x.AppliedState == TokenProjectionApplicationState.Confirmed && x.ConfirmedBlockHash == observation.BlockHash)
            .ToListAsync(token: cancellationToken);

        foreach (var application in applications)
        {
            await RemoveHistoryAsync(session, application.HistoryDocumentIds, cancellationToken);
            await RecomputeTouchedTokenStatesAsync(session, application.TokenIds, record.Sequence.Value, cancellationToken);
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
        var anyUnknown = transactions.Any(x => x.IsStas && !x.IsIssue && (!(x.AllStasInputsKnown) || (x.MissingTransactions?.Count ?? 0) > 0));
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

    private static string GetProtocolType(MetaTransaction transaction)
    {
        if ((transaction.Outputs ?? []).Any(x => x.Type == ScriptType.DSTAS) || !string.IsNullOrWhiteSpace(transaction.DstasEventType))
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
}
