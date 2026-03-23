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
        var addresses = new HashSet<string>();
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

        await SaveTransactionData(transaction);
        await SaveOutputs(inputs, outputs);

        var txValues = new Dictionary<string, object>
        {
            { nameof(MetaTransaction.Id), transaction.Id },

            { nameof(MetaTransaction.Block), blockHash },
            { nameof(MetaTransaction.Height), height },
            { nameof(MetaTransaction.Index), indexInBlock },
            { nameof(MetaTransaction.Timestamp), timestamp },

            { nameof(MetaTransaction.Addresses), addresses },
            { nameof(MetaTransaction.Outputs), slimOutputs },
            { nameof(MetaTransaction.Inputs), slimInputs },
            { nameof(MetaTransaction.IsStas), firstOutToRedeem != null || hasStasOutputs },
        };

        var patch = new PatchOperation(
            transaction.Id,
            null,
            new PatchRequest
            {
                Script = TransactionStorePatchScripts.UpdateMetaTransactionQuery,
                Values = txValues

            },
            new PatchRequest
            {
                Script = TransactionStorePatchScripts.InsertMetaTransactionQuery,
                Values = txValues
            }
        );

        var result = await store.Operations.SendAsync(patch);

        if (result == PatchStatus.Created && height == MetaTransaction.DefaultHeight)
        {
            return TransactionProcessStatus.FoundInMempool;
        }
        if (result == PatchStatus.Created && height != MetaTransaction.DefaultHeight)
        {
            return TransactionProcessStatus.FoundInBlock;
        }
        if (result == PatchStatus.Patched && height == MetaTransaction.DefaultHeight)
        {
            return TransactionProcessStatus.ReFoundInMempool;
        }
        if (result == PatchStatus.Patched && height != MetaTransaction.DefaultHeight)
        {
            return TransactionProcessStatus.UpdatedOnBlockConnected;
        }
        if (result == PatchStatus.NotModified)
        {
            return TransactionProcessStatus.NotModified;
        }

        return TransactionProcessStatus.Unexpected;
    }

    private async Task SaveTransactionData(Transaction transaction)
    {
        var values = new Dictionary<string, object>
        {
            { nameof(TransactionHexData.TxId), transaction.Id },
            { nameof(TransactionHexData.Hex), transaction.Hex },
        };

        var patch = new PatchOperation(
            TransactionHexData.GetId(transaction.Id),
            null,
            new PatchRequest
            {
                Script = "{}",
                Values = values

            },
            new PatchRequest
            {
                Script = TransactionStorePatchScripts.InsertTransactionHexData,
                Values = values
            }
        );

        await store.Operations.SendAsync(patch);
    }

    private async Task SaveOutputs(
        IEnumerable<MetaOutput> inputs,
        IEnumerable<MetaOutput> outputs
    )
    {
        foreach (var output in outputs)
        {
            var values = new Dictionary<string, object>
            {
                { nameof(MetaOutput.Id), output.Id },
                { nameof(MetaOutput.TxId), output.TxId },
                { nameof(MetaOutput.Vout), output.Vout },

                { nameof(MetaOutput.Type), output.Type.ToString("G") },
                { nameof(MetaOutput.Satoshis), output.Satoshis },
                { nameof(MetaOutput.Address), output.Address },
                { nameof(MetaOutput.TokenId), output.TokenId },
                { nameof(MetaOutput.Hash160), output.Hash160 },
                { nameof(MetaOutput.Symbol), output.Symbol },
                { nameof(MetaOutput.ScriptPubKey), output.ScriptPubKey },
                { nameof(MetaOutput.DstasFlags), output.DstasFlags },
                { nameof(MetaOutput.DstasFreezeEnabled), output.DstasFreezeEnabled },
                { nameof(MetaOutput.DstasConfiscationEnabled), output.DstasConfiscationEnabled },
                { nameof(MetaOutput.DstasFrozen), output.DstasFrozen },
                { nameof(MetaOutput.DstasFreezeAuthority), output.DstasFreezeAuthority },
                { nameof(MetaOutput.DstasConfiscationAuthority), output.DstasConfiscationAuthority },
                { nameof(MetaOutput.DstasServiceFields), output.DstasServiceFields },
                { nameof(MetaOutput.DstasActionType), output.DstasActionType },
                { nameof(MetaOutput.DstasActionData), output.DstasActionData },
                { nameof(MetaOutput.DstasRequestedScriptHash), output.DstasRequestedScriptHash },
                { nameof(MetaOutput.DstasOptionalData), output.DstasOptionalData },
                { nameof(MetaOutput.DstasOptionalDataFingerprint), output.DstasOptionalDataFingerprint },
            };

            var patchOutput = new PatchOperation(
                output.Id,
                null,
                new PatchRequest
                {
                    Script = TransactionStorePatchScripts.UpdateMetaOutputQuery,
                    Values = values
                },
                new PatchRequest
                {
                    Script = TransactionStorePatchScripts.InsertMetaOutputQuery,
                    Values = values
                }
            );

            await store.Operations.SendAsync(patchOutput);
        }

        foreach (var input in inputs)
        {
            var values = new Dictionary<string, object>
            {
                { nameof(MetaOutput.Id), input.Id },
                { nameof(MetaOutput.TxId), input.TxId },
                { nameof(MetaOutput.Vout), input.Vout },
                { nameof(MetaOutput.Type), input.Type.ToString("G") },
                { nameof(MetaOutput.InputIdx), input.InputIdx },
                { nameof(MetaOutput.SpendTxId), input.SpendTxId },
            };

            var patchInput = new PatchOperation(
                input.Id,
                null,
                new PatchRequest
                {
                    Script = TransactionStorePatchScripts.UpdateMetaInputQuery,
                    Values = values
                },
                new PatchRequest
                {
                    Script = TransactionStorePatchScripts.InsertMetaInputQuery,
                    Values = values
                }
            );

            await store.Operations.SendAsync(patchInput);
        }
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

            if (metaTx != null)
            {
                var metaOutputs = new List<MetaOutput>();
                foreach (var output in metaTx.Outputs)
                {
                    var metaOutput = await session.LoadAsync<MetaOutput>(output.Id);

                    if (metaOutput != null)
                    {
                        metaOutputs.Add(metaOutput);
                        session.Delete(metaOutput);
                    }
                }

                var rawData = await session.LoadAsync<TransactionHexData>(TransactionHexData.GetId(id));

                if (rawData != null)
                    session.Delete(rawData);

                session.Delete(metaTx);

                await session.SaveChangesAsync();

                var spentOutputIds = await session
                    .Outputs()
                    .Where(x => x.SpendTxId == id)
                    .Select(x => x.Id)
                    .ToListAsync();

                foreach (var outputId in spentOutputIds)
                {
                    await store.Operations.SendAsync(new PatchOperation(
                        outputId,
                        null,
                        new PatchRequest
                        {
                            Script = TransactionStorePatchScripts.FreeMetaOutputQuery,
                            Values =
                            {
                                { nameof(MetaOutput.SpendTxId), id },
                            }
                        }
                    ));
                }

                await mediator.Publish(new TransactionDeleted(id));

                var deletedTransaction = new DeletedTransaction
                {
                    Id = DeletedTransaction.GetId(id),
                    DeletedAt = DateTime.UtcNow,
                    MetaTransaction = metaTx,
                    MetaOutputs = metaOutputs,
                    RawData = rawData?.Hex
                };
                await session.StoreAsync(deletedTransaction);
                await session.SaveChangesAsync();

                var transaction = rawData != null
                    ? Transaction.Parse(rawData.Hex, networkProvider.Network)
                    : null;

                logger.LogError("Transaction was removed: {@Transaction}",
                    transaction != null
                        ? transaction
                        : metaTx
                );

                return transaction;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to delete transaction: {TxId}", id);
        }

        return null;
    }
}
