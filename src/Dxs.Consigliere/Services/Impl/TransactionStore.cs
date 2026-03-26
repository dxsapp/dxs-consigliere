using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Queries;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Notifications;

using MediatR;

using Microsoft.Extensions.Options;

using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Services.Impl;

public class TransactionStore(
    IDocumentStore store,
    IPublisher mediator,
    IOptions<TransactionFilterConfig> config,
    INetworkProvider networkProvider,
    ILogger<TransactionStore> logger
) : IMetaTransactionStore
{
    // Preserve the query contract surface used by contract tests while the script
    // implementation lives in the dedicated patch-script owner.
    private static readonly string UpdateStasAttributesQuery = TransactionStorePatchScripts.UpdateStasAttributesQuery;

    public async Task<List<Address>> GetWatchingAddresses()
    {
        using var session = store.GetSession();

        var result = config.Value.Addresses.Select(address => new Address(address)).ToList();
        var query = session
            .Query<WatchingAddress>()
            .Select(x => x.Address)
            .Distinct();

        await using var stream = await session.Advanced.StreamAsync(query);

        while (await stream.MoveNextAsync())
        {
            result.Add(new Address(stream.Current.Document));
        }

        return result;
    }

    public async Task<List<TokenId>> GetWatchingTokens()
    {
        using var session = store.GetSession();

        var result = new List<TokenId>();

        foreach (var token in config.Value.Tokens)
        {
            if (TokenId.TryParse(token, networkProvider.Network, out var tokenId))
                result.Add(tokenId);
            else
                throw new Exception($"Malformed tokenId in database: {token}");
        }

        var query = session.Query<WatchingToken>()
            .Select(x => x.TokenId)
            .Distinct();

        await using var stream = await session.Advanced.StreamAsync(query);

        while (await stream.MoveNextAsync())
        {
            if (TokenId.TryParse(stream.Current.Document, networkProvider.Network, out var tokenId))
                result.Add(tokenId);
            else
                throw new Exception($"Malformed tokenId in database: {stream.Current.Document}");
        }

        return result;
    }

    public async Task<TransactionProcessStatus> SaveTransaction(
        Transaction transaction,
        long timestamp,
        string firstOutToRedeem,
        string blockHash = null,
        int? blockHeight = null,
        int? indexInBlock = null
    )
    {
        var height = blockHeight ?? MetaTransaction.DefaultHeight;
        var inputs = new List<MetaOutput>();
        var slimInputs = new List<MetaTransaction.Input>();

        for (var i = 0; i < transaction.Inputs.Count; i++)
        {
            var input = transaction.Inputs[i];
            var metaInput = MetaOutput.FromInput(transaction, input, i, timestamp, height);

            inputs.Add(metaInput);
            slimInputs.Add(new MetaTransaction.Input(metaInput)
            {
                DstasSpendingType = input.DstasSpendingType
            });
        }

        var outputs = new List<MetaOutput>();
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        var slimOutputs = new List<MetaTransaction.Output>();
        var hasStasOutputs = false;

        foreach (var output in transaction.Outputs)
        {
            var metaOutput = MetaOutput.FromOutput(transaction, output, timestamp, height);

            outputs.Add(metaOutput);
            slimOutputs.Add(new MetaTransaction.Output(metaOutput));

            if (metaOutput.Address != null)
                addresses.Add(metaOutput.Address);

            if (output.Type is ScriptType.P2STAS or ScriptType.DSTAS)
                hasStasOutputs = true;
        }

        using var session = store.GetSession();

        await SaveTransactionDataAsync(session, transaction);
        await SaveOutputsAsync(session, inputs, outputs);
        var metaTransactionStatus = await UpsertMetaTransactionAsync(
            session,
            transaction.Id,
            timestamp,
            blockHash,
            height,
            indexInBlock,
            slimInputs,
            slimOutputs,
            addresses
        );

        await session.SaveChangesAsync();

        if (firstOutToRedeem != null || hasStasOutputs)
            await UpdateStasAttributes(transaction.Id);

        if (metaTransactionStatus == MetaTransactionWriteStatus.Created)
        {
            return height == MetaTransaction.DefaultHeight
                ? TransactionProcessStatus.FoundInMempool
                : TransactionProcessStatus.FoundInBlock;
        }

        if (metaTransactionStatus == MetaTransactionWriteStatus.NotModified)
            return TransactionProcessStatus.NotModified;

        return height == MetaTransaction.DefaultHeight
            ? TransactionProcessStatus.ReFoundInMempool
            : TransactionProcessStatus.UpdatedOnBlockConnected;
    }

    private static async Task SaveTransactionDataAsync(
        IAsyncDocumentSession session,
        Transaction transaction
    )
    {
        var id = TransactionHexData.GetId(transaction.Id);
        var existing = await session.LoadAsync<TransactionHexData>(id);

        if (existing != null)
        {
            if (!string.Equals(existing.Hex, transaction.Hex, StringComparison.Ordinal))
                existing.Hex = transaction.Hex;

            return;
        }

        await session.StoreAsync(
            new TransactionHexData
            {
                Id = id,
                TxId = transaction.Id,
                Hex = transaction.Hex
            },
            id
        );
    }

    private static async Task SaveOutputsAsync(
        IAsyncDocumentSession session,
        IReadOnlyCollection<MetaOutput> inputs,
        IReadOnlyCollection<MetaOutput> outputs
    )
    {
        var ids = outputs
            .Select(x => x.Id)
            .Concat(inputs.Select(x => x.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existing = ids.Length == 0
            ? new Dictionary<string, MetaOutput>()
            : await session.LoadAsync<MetaOutput>(ids);

        foreach (var output in outputs)
        {
            var current = existing.TryGetValue(output.Id, out var loaded) && loaded is not null
                ? loaded
                : new MetaOutput
                {
                    Id = output.Id,
                    TxId = output.TxId,
                    Vout = output.Vout
                };

            current.Vout = output.Vout;
            current.Type = output.Type;
            current.Satoshis = output.Satoshis;
            current.Address = output.Address;
            current.TokenId = output.TokenId;
            current.Hash160 = output.Hash160;
            current.Symbol = output.Symbol;
            current.ScriptPubKey = output.ScriptPubKey;
            current.DstasFlags = output.DstasFlags;
            current.DstasFreezeEnabled = output.DstasFreezeEnabled;
            current.DstasConfiscationEnabled = output.DstasConfiscationEnabled;
            current.DstasFrozen = output.DstasFrozen;
            current.DstasFreezeAuthority = output.DstasFreezeAuthority;
            current.DstasConfiscationAuthority = output.DstasConfiscationAuthority;
            current.DstasServiceFields = output.DstasServiceFields;
            current.DstasActionType = output.DstasActionType;
            current.DstasActionData = output.DstasActionData;
            current.DstasRequestedScriptHash = output.DstasRequestedScriptHash;
            current.DstasOptionalData = output.DstasOptionalData;
            current.DstasOptionalDataFingerprint = output.DstasOptionalDataFingerprint;

            if (loaded is null)
                await session.StoreAsync(current, current.Id);
        }

        foreach (var input in inputs)
        {
            var current = existing.TryGetValue(input.Id, out var loaded) && loaded is not null
                ? loaded
                : new MetaOutput
                {
                    Id = input.Id,
                    TxId = input.TxId,
                    Vout = input.Vout,
                    Type = input.Type
                };

            current.Vout = input.Vout;
            current.InputIdx = input.InputIdx;
            current.SpendTxId = input.SpendTxId;
            current.Spent = true;

            if (loaded is null)
                await session.StoreAsync(current, current.Id);
        }
    }

    private static async Task<MetaTransactionWriteStatus> UpsertMetaTransactionAsync(
        IAsyncDocumentSession session,
        string transactionId,
        long timestamp,
        string blockHash,
        int height,
        int? indexInBlock,
        IReadOnlyList<MetaTransaction.Input> slimInputs,
        IReadOnlyList<MetaTransaction.Output> slimOutputs,
        IReadOnlyCollection<string> addresses
    )
    {
        var existing = await session.LoadAsync<MetaTransaction>(transactionId);
        if (existing != null)
        {
            var changed = false;

            if (!string.Equals(existing.Block, blockHash, StringComparison.Ordinal))
            {
                existing.Block = blockHash;
                changed = true;
            }

            if (existing.Height != height)
            {
                existing.Height = height;
                changed = true;
            }

            var nextIndex = indexInBlock ?? 0;
            if (existing.Index != nextIndex)
            {
                existing.Index = nextIndex;
                changed = true;
            }

            if (existing.Timestamp == 0)
            {
                existing.Timestamp = timestamp;
                changed = true;
            }

            return changed
                ? MetaTransactionWriteStatus.Updated
                : MetaTransactionWriteStatus.NotModified;
        }

        await session.StoreAsync(new MetaTransaction
        {
            Id = transactionId,
            Block = blockHash,
            Height = height,
            Index = indexInBlock ?? 0,
            Timestamp = timestamp,
            Inputs = slimInputs.ToList(),
            Outputs = slimOutputs.ToList(),
            Addresses = addresses.ToList(),
            TokenIds = [],
            IllegalRoots = [],
            MissingTransactions = [],
            IsStas = false,
            IsIssue = false,
            IsValidIssue = false,
            IsRedeem = false,
            IsWithFee = false,
            IsWithNote = false,
            AllStasInputsKnown = false,
            RedeemAddress = null,
            StasFrom = null,
            Note = null,
            DstasEventType = null,
            DstasSpendingType = null,
            DstasInputFrozen = null,
            DstasOutputFrozen = null,
            DstasOptionalDataContinuity = null
        }, transactionId);

        return MetaTransactionWriteStatus.Created;
    }

    public async Task UpdateStasAttributes(string txId)
    {
        try
        {
            var patchRequest = new PatchRequest
            {
                Script = TransactionStorePatchScripts.UpdateStasAttributesQuery
            };
            var patch = new PatchOperation(
                txId,
                null,
                patchRequest
            );

            await store.Operations.SendAsync(patch);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update transaction attributes failed");
        }
    }

    public async Task<Transaction> TryRemoveTransaction(string id)
    {
        try
        {
            using var session = store.GetSession();
            var metaTx = await session
                .Include<MetaTransaction>(x => x.Outputs.Select(y => y.Id))
                .LoadAsync<MetaTransaction>(id);

            if (metaTx == null)
                return null;

            var metaOutputs = new List<MetaOutput>();
            var outputIds = (metaTx.Outputs ?? [])
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (outputIds.Length > 0)
            {
                var loadedOutputs = await session.LoadAsync<MetaOutput>(outputIds);
                foreach (var pair in loadedOutputs)
                {
                    if (pair.Value == null)
                        continue;

                    metaOutputs.Add(pair.Value);
                    session.Delete(pair.Value);
                }
            }

            var rawData = await session.LoadAsync<TransactionHexData>(TransactionHexData.GetId(id));
            if (rawData != null)
                session.Delete(rawData);

            var spentOutputs = await session
                .Outputs()
                .Where(x => x.SpendTxId == id)
                .ToListAsync();

            foreach (var spentOutput in spentOutputs)
            {
                spentOutput.SpendTxId = null;
                spentOutput.InputIdx = 0;
                spentOutput.Spent = false;
            }

            var transaction = rawData != null
                ? Transaction.Parse(rawData.Hex, networkProvider.Network)
                : null;

            var deletedTransaction = new DeletedTransaction
            {
                Id = DeletedTransaction.GetId(id),
                DeletedAt = DateTime.UtcNow,
                MetaTransaction = metaTx,
                MetaOutputs = metaOutputs,
                RawData = rawData?.Hex
            };

            session.Delete(metaTx);
            await session.StoreAsync(deletedTransaction);
            await session.SaveChangesAsync();

            await mediator.Publish(new TransactionDeleted(id));

            logger.LogError("Transaction was removed: {@Transaction}",
                transaction != null
                    ? transaction
                    : metaTx
            );

            return transaction;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to delete transaction: {TxId}", id);
        }

        return null;
    }

    private enum MetaTransactionWriteStatus
    {
        Created,
        Updated,
        NotModified
    }
}
