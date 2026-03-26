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
        var application = await session.LoadAsync<AddressProjectionAppliedTransactionDocument>(
                AddressProjectionAppliedTransactionDocument.GetId(observation.TxId),
                cancellationToken)
            ?? new AddressProjectionAppliedTransactionDocument
            {
                Id = AddressProjectionAppliedTransactionDocument.GetId(observation.TxId),
                TxId = observation.TxId
            };

        switch (observation.EventType)
        {
            case TxObservationEventType.SeenInMempool:
                if (string.Equals(application.AppliedState, AddressProjectionApplicationState.Confirmed, StringComparison.Ordinal))
                {
                    Touch(application, record, observation);
                    await session.StoreAsync(application, application.Id, cancellationToken);
                    return true;
                }

                if (string.Equals(application.AppliedState, AddressProjectionApplicationState.Pending, StringComparison.Ordinal))
                {
                    Touch(application, record, observation);
                    await session.StoreAsync(application, application.Id, cancellationToken);
                    return true;
                }

                if (!await TryApplyTransactionAsync(session, application, record, observation, AddressProjectionApplicationState.Pending, cancellationToken))
                    return false;

                return true;

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

                if (!await TryApplyTransactionAsync(session, application, record, observation, AddressProjectionApplicationState.Confirmed, cancellationToken))
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

                await RevertApplicationAsync(session, application, record.Sequence.Value, cancellationToken);
                Touch(application, record, observation);
                await session.StoreAsync(application, application.Id, cancellationToken);
                return true;
        }

        return true;
    }

    private static async Task<bool> TryApplyTransactionAsync(
        IAsyncDocumentSession session,
        AddressProjectionAppliedTransactionDocument application,
        StoredObservationJournalRecord record,
        TxObservation observation,
        string targetState,
        CancellationToken cancellationToken
    )
    {
        var metaTransaction = await session.LoadAsync<MetaTransaction>(observation.TxId, cancellationToken);
        if (metaTransaction is null)
            return false;

        var outputIds = (metaTransaction.Outputs ?? []).Select(x => x.Id).ToArray();
        var loadedOutputs = outputIds.Length == 0
            ? []
            : (await session.LoadAsync<MetaOutput>(outputIds, cancellationToken)).Values
                .Where(x => x is not null)
                .ToArray();

        if (loadedOutputs.Length != outputIds.Length)
            return false;

        var credits = loadedOutputs
            .Where(x => ShouldProjectOutput(metaTransaction, x))
            .Select(AddressProjectionUtxoSnapshot.From)
            .ToArray();

        var debits = new List<AddressProjectionUtxoSnapshot>();
        foreach (var input in metaTransaction.Inputs ?? [])
        {
            var debitDocument = await session.LoadAsync<AddressUtxoProjectionDocument>(
                AddressUtxoProjectionDocument.GetId(input.TxId, input.Vout),
                cancellationToken
            );

            if (debitDocument is not null)
                debits.Add(debitDocument.ToSnapshot());
        }

        await ApplyMutationAsync(session, debits, credits, record.Sequence.Value, cancellationToken);

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
        StoredObservationJournalRecord record,
        BlockObservation observation,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(observation.EventType, BlockObservationEventType.Disconnected, StringComparison.Ordinal))
            return;

        var applications = await session.Query<AddressProjectionAppliedTransactionDocument>()
            .Where(x => x.AppliedState == AddressProjectionApplicationState.Confirmed && x.ConfirmedBlockHash == observation.BlockHash)
            .ToListAsync(token: cancellationToken);

        foreach (var application in applications)
        {
            await RevertApplicationAsync(session, application, record.Sequence.Value, cancellationToken);
            application.LastObservedAt = observation.ObservedAt ?? record.AppendedAt;
            await session.StoreAsync(application, application.Id, cancellationToken);
        }
    }

    private static async Task ApplyMutationAsync(
        IAsyncDocumentSession session,
        IReadOnlyCollection<AddressProjectionUtxoSnapshot> debits,
        IReadOnlyCollection<AddressProjectionUtxoSnapshot> credits,
        long sequence,
        CancellationToken cancellationToken
    )
    {
        foreach (var debit in debits)
        {
            await DeleteUtxoAsync(session, debit, cancellationToken);
            await ApplyBalanceDeltaAsync(session, debit.Address, debit.TokenId, -debit.Satoshis, sequence, cancellationToken);
        }

        foreach (var credit in credits)
        {
            await UpsertUtxoAsync(session, credit, sequence, cancellationToken);
            await ApplyBalanceDeltaAsync(session, credit.Address, credit.TokenId, credit.Satoshis, sequence, cancellationToken);
        }
    }

    private static async Task RevertApplicationAsync(
        IAsyncDocumentSession session,
        AddressProjectionAppliedTransactionDocument application,
        long sequence,
        CancellationToken cancellationToken
    )
    {
        foreach (var credit in application.Credits ?? [])
        {
            await DeleteUtxoAsync(session, credit, cancellationToken);
            await ApplyBalanceDeltaAsync(session, credit.Address, credit.TokenId, -credit.Satoshis, sequence, cancellationToken);
        }

        foreach (var debit in application.Debits ?? [])
        {
            await UpsertUtxoAsync(session, debit, sequence, cancellationToken);
            await ApplyBalanceDeltaAsync(session, debit.Address, debit.TokenId, debit.Satoshis, sequence, cancellationToken);
        }

        application.AppliedState = AddressProjectionApplicationState.None;
        application.ConfirmedBlockHash = null;
        application.Credits = [];
        application.Debits = [];
        application.LastSequence = sequence;
    }

    private static async Task UpsertUtxoAsync(
        IAsyncDocumentSession session,
        AddressProjectionUtxoSnapshot snapshot,
        long sequence,
        CancellationToken cancellationToken
    )
    {
        var id = AddressUtxoProjectionDocument.GetId(snapshot.TxId, snapshot.Vout);
        var existing = await session.LoadAsync<AddressUtxoProjectionDocument>(id, cancellationToken);
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

        await session.StoreAsync(AddressUtxoProjectionDocument.From(snapshot, sequence), id, cancellationToken);
    }

    private static async Task DeleteUtxoAsync(
        IAsyncDocumentSession session,
        AddressProjectionUtxoSnapshot snapshot,
        CancellationToken cancellationToken
    )
    {
        var id = AddressUtxoProjectionDocument.GetId(snapshot.TxId, snapshot.Vout);
        var existing = await session.LoadAsync<AddressUtxoProjectionDocument>(id, cancellationToken);
        if (existing is not null)
            session.Delete(existing);
    }

    private static async Task ApplyBalanceDeltaAsync(
        IAsyncDocumentSession session,
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
        var balance = await session.LoadAsync<AddressBalanceProjectionDocument>(id, cancellationToken);
        if (balance is null)
        {
            if (delta == 0)
                return;

            balance = new AddressBalanceProjectionDocument
            {
                Id = id,
                Address = address,
                TokenId = tokenId,
                Satoshis = 0
            };

            await session.StoreAsync(balance, id, cancellationToken);
        }

        balance.Satoshis += delta;
        balance.LastSequence = sequence;

        if (balance.Satoshis == 0)
            session.Delete(balance);
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
}
