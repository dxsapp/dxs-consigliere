using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Data.Addresses;

public sealed class AddressProjectionRebuilder(
    IDocumentStore documentStore,
    RavenObservationJournalReader journalReader
)
{
    private const int DefaultPageSize = 512;

    public async Task<ProjectionCheckpoint> RebuildAsync(
        int take = DefaultPageSize,
        CancellationToken cancellationToken = default
    )
    {
        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take));

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

            await StoreCheckpointAsync(session, checkpoint, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        return checkpoint;
    }

    private async Task<bool> ApplyAsync(
        IAsyncDocumentSession session,
        AddressProjectionBatchContext batchContext,
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
        AddressProjectionBatchContext batchContext,
        StoredObservationJournalRecord record,
        TxObservation observation,
        CancellationToken cancellationToken
    )
    {
        if (!batchContext.Applications.TryGetValue(observation.TxId, out var application))
        {
            application = new AddressProjectionAppliedTransactionDocument
            {
                Id = AddressProjectionAppliedTransactionDocument.GetId(observation.TxId),
                TxId = observation.TxId
            };

            batchContext.Applications[observation.TxId] = application;
        }

        switch (observation.EventType)
        {
            case TxObservationEventType.SeenInMempool:
                if (string.Equals(application.AppliedState, AddressProjectionApplicationState.Confirmed, StringComparison.Ordinal)
                    || string.Equals(application.AppliedState, AddressProjectionApplicationState.Pending, StringComparison.Ordinal))
                {
                    Touch(application, record, observation);
                    await session.StoreAsync(application, application.Id, cancellationToken);
                    return true;
                }

                return await TryApplyTransactionAsync(
                    session,
                    batchContext,
                    application,
                    record,
                    observation,
                    AddressProjectionApplicationState.Pending,
                    cancellationToken);

            case TxObservationEventType.SeenInBlock:
                if (string.Equals(application.AppliedState, AddressProjectionApplicationState.Confirmed, StringComparison.Ordinal))
                {
                    application.ConfirmedBlockHash = observation.BlockHash;
                    Touch(application, record, observation);
                    await session.StoreAsync(application, application.Id, cancellationToken);
                    return true;
                }

                if (string.Equals(application.AppliedState, AddressProjectionApplicationState.Pending, StringComparison.Ordinal))
                {
                    application.AppliedState = AddressProjectionApplicationState.Confirmed;
                    application.ConfirmedBlockHash = observation.BlockHash;
                    Touch(application, record, observation);
                    await session.StoreAsync(application, application.Id, cancellationToken);
                    return true;
                }

                if (!await TryApplyTransactionAsync(
                        session,
                        batchContext,
                        application,
                        record,
                        observation,
                        AddressProjectionApplicationState.Confirmed,
                        cancellationToken))
                    return false;

                application.ConfirmedBlockHash = observation.BlockHash;
                await session.StoreAsync(application, application.Id, cancellationToken);
                return true;

            case TxObservationEventType.DroppedBySource:
                if (!string.Equals(application.AppliedState, AddressProjectionApplicationState.Pending, StringComparison.Ordinal))
                {
                    Touch(application, record, observation);
                    await session.StoreAsync(application, application.Id, cancellationToken);
                    return true;
                }

                await RevertApplicationAsync(session, batchContext, application, record.Sequence.Value, cancellationToken);
                Touch(application, record, observation);
                await session.StoreAsync(application, application.Id, cancellationToken);
                return true;
        }

        return true;
    }

    private static async Task<bool> TryApplyTransactionAsync(
        IAsyncDocumentSession session,
        AddressProjectionBatchContext batchContext,
        AddressProjectionAppliedTransactionDocument application,
        StoredObservationJournalRecord record,
        TxObservation observation,
        string targetState,
        CancellationToken cancellationToken
    )
    {
        if (!batchContext.MetaTransactions.TryGetValue(observation.TxId, out var metaTransaction))
            return false;

        var outputIds = (metaTransaction.Outputs ?? []).Select(x => x.Id).ToArray();
        var loadedOutputs = outputIds
            .Select(id => batchContext.MetaOutputs.TryGetValue(id, out var output) ? output : null)
            .Where(x => x is not null)
            .ToArray();

        if (loadedOutputs.Length != outputIds.Length)
            return false;

        var credits = loadedOutputs
            .Where(x => ShouldProjectOutput(metaTransaction, x!))
            .Select(AddressProjectionUtxoSnapshot.From)
            .ToArray();

        var debits = new List<AddressProjectionUtxoSnapshot>();
        foreach (var input in metaTransaction.Inputs ?? [])
        {
            var debitDocument = await GetOrLoadUtxoAsync(session, batchContext, input.TxId, input.Vout, cancellationToken);
            if (debitDocument is not null)
                debits.Add(debitDocument.ToSnapshot());
        }

        await ApplyMutationAsync(session, batchContext, debits, credits, record.Sequence.Value, cancellationToken);

        application.AppliedState = targetState;
        application.ConfirmedBlockHash = targetState == AddressProjectionApplicationState.Confirmed
            ? observation.BlockHash
            : null;
        application.Credits = credits;
        application.Debits = debits.ToArray();
        Touch(application, record, observation);

        await session.StoreAsync(application, application.Id, cancellationToken);
        return true;
    }

    private static async Task ApplyBlockObservationAsync(
        IAsyncDocumentSession session,
        AddressProjectionBatchContext batchContext,
        StoredObservationJournalRecord record,
        BlockObservation observation,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(observation.EventType, BlockObservationEventType.Disconnected, StringComparison.Ordinal))
            return;

        var applications = batchContext.Applications.Values
            .Where(x => string.Equals(x.AppliedState, AddressProjectionApplicationState.Confirmed, StringComparison.Ordinal)
                && string.Equals(x.ConfirmedBlockHash, observation.BlockHash, StringComparison.Ordinal))
            .ToList();

        if (applications.Count == 0)
        {
            applications = await session.Query<AddressProjectionAppliedTransactionDocument>()
                .Where(x => x.AppliedState == AddressProjectionApplicationState.Confirmed && x.ConfirmedBlockHash == observation.BlockHash)
                .ToListAsync(token: cancellationToken);

            foreach (var application in applications)
                batchContext.Applications[application.TxId] = application;
        }

        foreach (var application in applications)
        {
            await RevertApplicationAsync(session, batchContext, application, record.Sequence.Value, cancellationToken);
            application.LastObservedAt = observation.ObservedAt ?? record.AppendedAt;
            await session.StoreAsync(application, application.Id, cancellationToken);
        }
    }

    private static async Task ApplyMutationAsync(
        IAsyncDocumentSession session,
        AddressProjectionBatchContext batchContext,
        IReadOnlyCollection<AddressProjectionUtxoSnapshot> debits,
        IReadOnlyCollection<AddressProjectionUtxoSnapshot> credits,
        long sequence,
        CancellationToken cancellationToken
    )
    {
        foreach (var debit in debits)
        {
            await DeleteUtxoAsync(session, batchContext, debit, cancellationToken);
            await ApplyBalanceDeltaAsync(session, batchContext, debit.Address, debit.TokenId, -debit.Satoshis, sequence, cancellationToken);
        }

        foreach (var credit in credits)
        {
            await UpsertUtxoAsync(session, batchContext, credit, sequence, cancellationToken);
            await ApplyBalanceDeltaAsync(session, batchContext, credit.Address, credit.TokenId, credit.Satoshis, sequence, cancellationToken);
        }
    }

    private static async Task RevertApplicationAsync(
        IAsyncDocumentSession session,
        AddressProjectionBatchContext batchContext,
        AddressProjectionAppliedTransactionDocument application,
        long sequence,
        CancellationToken cancellationToken
    )
    {
        foreach (var credit in application.Credits ?? [])
        {
            await DeleteUtxoAsync(session, batchContext, credit, cancellationToken);
            await ApplyBalanceDeltaAsync(session, batchContext, credit.Address, credit.TokenId, -credit.Satoshis, sequence, cancellationToken);
        }

        foreach (var debit in application.Debits ?? [])
        {
            await UpsertUtxoAsync(session, batchContext, debit, sequence, cancellationToken);
            await ApplyBalanceDeltaAsync(session, batchContext, debit.Address, debit.TokenId, debit.Satoshis, sequence, cancellationToken);
        }

        application.AppliedState = AddressProjectionApplicationState.None;
        application.ConfirmedBlockHash = null;
        application.Credits = [];
        application.Debits = [];
        application.LastSequence = sequence;
    }

    private static async Task UpsertUtxoAsync(
        IAsyncDocumentSession session,
        AddressProjectionBatchContext batchContext,
        AddressProjectionUtxoSnapshot snapshot,
        long sequence,
        CancellationToken cancellationToken
    )
    {
        var id = AddressUtxoProjectionDocument.GetId(snapshot.TxId, snapshot.Vout);
        var existing = await GetOrLoadUtxoByIdAsync(session, batchContext, id, cancellationToken);
        if (existing is not null)
        {
            existing.Address = snapshot.Address;
            existing.TokenId = snapshot.TokenId;
            existing.Satoshis = snapshot.Satoshis;
            existing.ScriptType = snapshot.ScriptType;
            existing.ScriptPubKey = snapshot.ScriptPubKey;
            existing.LastSequence = sequence;
            return;
        }

        var document = AddressUtxoProjectionDocument.From(snapshot, sequence);
        batchContext.Utxos[id] = document;
        await session.StoreAsync(document, id, cancellationToken);
    }

    private static async Task DeleteUtxoAsync(
        IAsyncDocumentSession session,
        AddressProjectionBatchContext batchContext,
        AddressProjectionUtxoSnapshot snapshot,
        CancellationToken cancellationToken
    )
    {
        var id = AddressUtxoProjectionDocument.GetId(snapshot.TxId, snapshot.Vout);
        var existing = await GetOrLoadUtxoByIdAsync(session, batchContext, id, cancellationToken);
        if (existing is not null)
        {
            batchContext.Utxos[id] = null;
            session.Delete(existing);
        }
    }

    private static async Task ApplyBalanceDeltaAsync(
        IAsyncDocumentSession session,
        AddressProjectionBatchContext batchContext,
        string address,
        string tokenId,
        long delta,
        long sequence,
        CancellationToken cancellationToken
    )
    {
        if (delta == 0 || string.IsNullOrWhiteSpace(address))
            return;

        var id = AddressBalanceProjectionDocument.GetId(address, tokenId);
        if (!batchContext.Balances.TryGetValue(id, out var balance))
        {
            balance = await session.LoadAsync<AddressBalanceProjectionDocument>(id, cancellationToken);
            batchContext.Balances[id] = balance;
        }

        if (balance is null)
        {
            balance = new AddressBalanceProjectionDocument
            {
                Id = id,
                Address = address,
                TokenId = tokenId,
                Satoshis = 0
            };

            batchContext.Balances[id] = balance;
            await session.StoreAsync(balance, id, cancellationToken);
        }

        balance.Satoshis += delta;
        balance.LastSequence = sequence;

        if (balance.Satoshis == 0)
        {
            batchContext.Balances[id] = null;
            session.Delete(balance);
        }
    }

    private static async Task<AddressProjectionBatchContext> LoadBatchContextAsync(
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

        var applications = new Dictionary<string, AddressProjectionAppliedTransactionDocument>(StringComparer.OrdinalIgnoreCase);
        if (txIds.Length > 0)
        {
            var loaded = await session.LoadAsync<AddressProjectionAppliedTransactionDocument>(
                txIds.Select(AddressProjectionAppliedTransactionDocument.GetId),
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

        var outputIds = metaTransactions.Values
            .SelectMany(x => x.Outputs ?? [])
            .Select(x => x.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var metaOutputs = new Dictionary<string, MetaOutput>(StringComparer.OrdinalIgnoreCase);
        if (outputIds.Length > 0)
        {
            var loaded = await session.LoadAsync<MetaOutput>(outputIds, cancellationToken);
            foreach (var output in loaded)
            {
                if (output.Value is not null)
                    metaOutputs[output.Key] = output.Value;
            }
        }

        var utxoIds = metaTransactions.Values
            .SelectMany(x => x.Inputs ?? [])
            .Select(x => AddressUtxoProjectionDocument.GetId(x.TxId, x.Vout))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var utxos = new Dictionary<string, AddressUtxoProjectionDocument>(StringComparer.OrdinalIgnoreCase);
        if (utxoIds.Length > 0)
        {
            var loaded = await session.LoadAsync<AddressUtxoProjectionDocument>(utxoIds, cancellationToken);
            foreach (var utxo in loaded)
            {
                if (utxo.Value is not null)
                    utxos[utxo.Key] = utxo.Value;
            }
        }

        return new AddressProjectionBatchContext(
            applications,
            metaTransactions,
            metaOutputs,
            utxos,
            new Dictionary<string, AddressBalanceProjectionDocument>(StringComparer.OrdinalIgnoreCase));
    }

    private static Task<AddressUtxoProjectionDocument> GetOrLoadUtxoAsync(
        IAsyncDocumentSession session,
        AddressProjectionBatchContext batchContext,
        string txId,
        int vout,
        CancellationToken cancellationToken
    ) => GetOrLoadUtxoByIdAsync(session, batchContext, AddressUtxoProjectionDocument.GetId(txId, vout), cancellationToken);

    private static async Task<AddressUtxoProjectionDocument> GetOrLoadUtxoByIdAsync(
        IAsyncDocumentSession session,
        AddressProjectionBatchContext batchContext,
        string id,
        CancellationToken cancellationToken
    )
    {
        if (batchContext.Utxos.TryGetValue(id, out var existing))
            return existing;

        existing = await session.LoadAsync<AddressUtxoProjectionDocument>(id, cancellationToken);
        batchContext.Utxos[id] = existing;
        return existing;
    }

    private static bool ShouldProjectOutput(MetaTransaction transaction, MetaOutput output)
    {
        if (output is null || string.IsNullOrWhiteSpace(output.Address))
            return false;

        return output.Type switch
        {
            Dxs.Bsv.Script.ScriptType.P2PKH => true,
            Dxs.Bsv.Script.ScriptType.P2MPKH => true,
            Dxs.Bsv.Script.ScriptType.P2STAS => transaction.IsValidIssue || (transaction.AllStasInputsKnown && !transaction.IllegalRoots.Any()),
            Dxs.Bsv.Script.ScriptType.DSTAS => transaction.IsValidIssue || (transaction.AllStasInputsKnown && !transaction.IllegalRoots.Any()),
            _ => false
        };
    }

    private static void Touch(
        AddressProjectionAppliedTransactionDocument application,
        StoredObservationJournalRecord record,
        TxObservation observation
    )
    {
        application.LastObservedAt = observation.ObservedAt ?? record.AppendedAt;
        application.LastSequence = record.Sequence.Value;
    }

    private async Task<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken cancellationToken)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var checkpoint = await session.LoadAsync<AddressProjectionCheckpointDocument>(
            AddressProjectionCheckpointDocument.DocumentId,
            cancellationToken
        );

        return checkpoint is null
            ? new ProjectionCheckpoint(JournalSequence.Empty)
            : new ProjectionCheckpoint(
                new JournalSequence(checkpoint.LastSequence),
                string.IsNullOrWhiteSpace(checkpoint.LastFingerprint)
                    ? null
                    : new DedupeFingerprint(checkpoint.LastFingerprint)
            );
    }

    private static Task StoreCheckpointAsync(
        IAsyncDocumentSession session,
        ProjectionCheckpoint checkpoint,
        CancellationToken cancellationToken
    )
        => session.StoreAsync(
            new AddressProjectionCheckpointDocument
            {
                Id = AddressProjectionCheckpointDocument.DocumentId,
                LastSequence = checkpoint.Sequence.Value,
                LastFingerprint = checkpoint.LastAppliedFingerprint?.Value
            },
            AddressProjectionCheckpointDocument.DocumentId,
            cancellationToken
        );

    private sealed record AddressProjectionBatchContext(
        Dictionary<string, AddressProjectionAppliedTransactionDocument> Applications,
        Dictionary<string, MetaTransaction> MetaTransactions,
        Dictionary<string, MetaOutput> MetaOutputs,
        Dictionary<string, AddressUtxoProjectionDocument> Utxos,
        Dictionary<string, AddressBalanceProjectionDocument> Balances
    );
}
