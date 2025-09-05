using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Queries;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Notifications;
using MediatR;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;

namespace Dxs.Consigliere.Services.Impl;

public class TransactionStore(
    IDocumentStore store,
    IPublisher mediator,
    ILogger<TransactionStore> logger
): IMetaTransactionStore
{
    public async Task<List<Address>> GetWatchingAddresses()
    {
        using var session = store.GetSession();

        var result = new List<Address>();

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

        var query = session.Query<WatchingToken>()
            .Select(x => x.TokenId)
            .Distinct();

        await using var stream = await session.Advanced.StreamAsync(query);

        while (await stream.MoveNextAsync())
        {
            if (TokenId.TryParse(stream.Current.Document, Network.Mainnet, out var tokenId))
                result.Add(tokenId);
            else
                throw new Exception($"Malformed tokenId in database: {stream.Current.Document}");
        }

        return result;
    }

    private static readonly string UpdateStasAttributesQuery = $@"
var stasInputsCount = 0;
var inputsCount = this.{nameof(MetaTransaction.Inputs)}.length;
var outputsCount = this.{nameof(MetaTransaction.Outputs)}.length;
var withNote = this.{nameof(MetaTransaction.Outputs)}[outputsCount - 1].{nameof(MetaTransaction.Output.Type)} === '{ScriptType.NullData:G}';
var feeIdx = withNote 
    ? outputsCount - 2 
    : outputsCount - 1;
var withFee = false;
var mustKnowInputsCount = inputsCount;
var allInputsKnown = true;
var stasFrom;
var firstInputHash160 = null;
var firstInputTokenId = null;
var inputTokens = new Set();
var illegalRoots = new Set();
var missingTxs = new Set();
var redeemAddress = null;
var outputTokens = new Set();

for (var i = 0; i < outputsCount; i++) {{
    var output = this.{nameof(MetaTransaction.Outputs)}[i];

    if (output.{nameof(MetaTransaction.Output.Type)} === '{ScriptType.P2STAS:G}') {{
        outputTokens.add(output.{nameof(MetaTransaction.Output.TokenId)});
    }}

    if (i === 0 && output.{nameof(MetaTransaction.Output.Type)} === '{ScriptType.P2PKH:G}') {{
        redeemAddress = output.{nameof(MetaTransaction.Output.Address)};
    }}
}}

outputTokens = [...outputTokens];

for (var i = 0; i < inputsCount; i++) {{
    var slimInput = this.{nameof(MetaTransaction.Inputs)}[i];
    var inputTxId = slimInput.{nameof(MetaTransaction.Input.TxId)};
    var inputTx = load(inputTxId);

    if (!inputTx) {{
        allInputsKnown = false;
        missingTxs.add(inputTxId);
        continue;
    }}

    allInputsKnown = allInputsKnown && true;

    var vout = slimInput.{nameof(MetaTransaction.Input.Vout)};
    var inputOutput = inputTx.{nameof(MetaTransaction.Outputs)}[vout];
    var isInputStas = inputOutput.{nameof(MetaTransaction.Output.Type)} === '{ScriptType.P2STAS:G}';

    if (i === 0) {{
        firstInputHash160 = inputOutput.{nameof(MetaTransaction.Output.Hash160)};
    }}
    else if (i === inputsCount - 1) {{
        withFee = inputOutput.{nameof(MetaTransaction.Output.Type)} === '{ScriptType.P2PKH:G}'
    }}

    if (isInputStas) {{
        stasInputsCount++;

        if (i === 0) {{
            stasFrom = inputOutput.{nameof(MetaTransaction.Output.Address)};
            firstInputTokenId = inputOutput.{nameof(MetaTransaction.Output.TokenId)};
        }}

        if (inputTx.{nameof(MetaTransaction.MissingTransactions)}.length > 0) {{
            missingTxs.add(inputTxId);
        }}

        inputTokens.add(inputOutput.{nameof(MetaTransaction.Output.TokenId)});

        if (inputTx.{nameof(MetaTransaction.IsIssue)} === true) {{
            if (inputTx.{nameof(MetaTransaction.IsValidIssue)} !== true) {{
                illegalRoots.add(inputTxId);
            }}
        }} else {{
            for (var j = 0; j < inputTx.{nameof(MetaTransaction.IllegalRoots)}.length; j++) {{
                var illegalRoot = inputTx.{nameof(MetaTransaction.IllegalRoots)}[j];
                illegalRoots.add(illegalRoot);
            }}
        }}
    }}
}}

var hasStasOutputs = outputTokens.length > 0;
var isStas = hasStasOutputs || stasInputsCount > 0;
var isIssue = isStas && hasStasOutputs && stasInputsCount === 0;
var isValidIssue = isIssue && allInputsKnown && outputTokens.length === 1 && outputTokens[0] === firstInputHash160;
var isRedeem = allInputsKnown
    && stasInputsCount === 1 
    && stasFrom === this.{nameof(MetaTransaction.Outputs)}[0].{nameof(MetaTransaction.Output.Address)}
    && firstInputTokenId === this.{nameof(MetaTransaction.Outputs)}[0].{nameof(MetaTransaction.Output.Hash160)};

this.{nameof(MetaTransaction.IsStas)} = isStas;
this.{nameof(MetaTransaction.IsIssue)} = isIssue;
this.{nameof(MetaTransaction.IsValidIssue)} = isValidIssue;
this.{nameof(MetaTransaction.IsRedeem)} = isRedeem;
this.{nameof(MetaTransaction.IsWithFee)} = isStas && withFee;
this.{nameof(MetaTransaction.IsWithNote)} = isStas && withNote;
this.{nameof(MetaTransaction.AllStasInputsKnown)} = isStas && allInputsKnown;
this.{nameof(MetaTransaction.RedeemAddress)} = isRedeem ? redeemAddress : null;
this.{nameof(MetaTransaction.StasFrom)} = isStas ? stasFrom : null;

this.{nameof(MetaTransaction.TokenIds)} = [...new Set([...outputTokens, ...inputTokens])];
this.{nameof(MetaTransaction.IllegalRoots)} = [...illegalRoots];
this.{nameof(MetaTransaction.MissingTransactions)} = [...missingTxs];
";

    private static readonly string InsertMetaTransactionQuery = $@"
this.{nameof(MetaTransaction.Block)} = ${nameof(MetaTransaction.Block)};
this.{nameof(MetaTransaction.Height)} = ${nameof(MetaTransaction.Height)};
this.{nameof(MetaTransaction.Index)} = ${nameof(MetaTransaction.Index)};
this.{nameof(MetaTransaction.Timestamp)} = ${nameof(MetaTransaction.Timestamp)};

this.{nameof(MetaTransaction.Inputs)} = ${nameof(MetaTransaction.Inputs)};
this.{nameof(MetaTransaction.Outputs)} = ${nameof(MetaTransaction.Outputs)};
this.{nameof(MetaTransaction.Addresses)} = ${nameof(MetaTransaction.Addresses)};

this.{nameof(MetaTransaction.Note)} = ${nameof(MetaTransaction.Note)};

this.{nameof(MetaTransaction.IsStas)} = false;
this.{nameof(MetaTransaction.IsIssue)} = false;
this.{nameof(MetaTransaction.IsValidIssue)} = false;
this.{nameof(MetaTransaction.IsRedeem)} = false;
this.{nameof(MetaTransaction.AllStasInputsKnown)} = false;
this.{nameof(MetaTransaction.RedeemAddress)} = null;
this.{nameof(MetaTransaction.StasFrom)} = null;

this.{nameof(MetaTransaction.TokenIds)} = [];
this.{nameof(MetaTransaction.IllegalRoots)} = [];
this.{nameof(MetaTransaction.MissingTransactions)} = [];

if (${nameof(MetaTransaction.IsStas)}) {{
    {UpdateStasAttributesQuery}
}}

this['@metadata'] = {{ 
    '@collection': 'MetaTransactions', 
    'Raven-Clr-Type': '{typeof(MetaTransaction).FullName}, {typeof(MetaTransaction).Assembly.GetName().Name}' 
}};
";

    private static readonly string UpdateMetaTransactionQuery = $@"
this.{nameof(MetaTransaction.Block)} = ${nameof(MetaTransaction.Block)};
this.{nameof(MetaTransaction.Index)} = ${nameof(MetaTransaction.Index)};
this.{nameof(MetaTransaction.Height)} = ${nameof(MetaTransaction.Height)};

if (!this.{nameof(MetaTransaction.Timestamp)}) {{
    this.{nameof(MetaTransaction.Timestamp)} = ${nameof(MetaTransaction.Timestamp)};
}}

if (${nameof(MetaTransaction.IsStas)}) {{
    {UpdateStasAttributesQuery}
}}
";

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
            slimInputs.Add(new MetaTransaction.Input(metaInput));
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

            if (output.Type == ScriptType.P2STAS)
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
                Script = UpdateMetaTransactionQuery,
                Values = txValues

            },
            new PatchRequest
            {
                Script = InsertMetaTransactionQuery,
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
                Script = InsertTransactionHexData,
                Values = values
            }
        );

        await store.Operations.SendAsync(patch);
    }

    private static readonly string InsertTransactionHexData = $@"
this.{nameof(TransactionHexData.TxId)} = ${nameof(TransactionHexData.TxId)};
this.{nameof(TransactionHexData.Hex)} = ${nameof(TransactionHexData.Hex)};

this['@metadata'] = {{ 
    '@collection': 'TransactionHexDatas', 
    'Raven-Clr-Type': '{typeof(TransactionHexData).FullName}, {typeof(TransactionHexData).Assembly.GetName().Name}' 
}};
";

    public static readonly string InsertOutputQuery = $@"
this.{nameof(MetaOutput.TxId)} = ${nameof(MetaOutput.TxId)};
this.{nameof(MetaOutput.Vout)} = ${nameof(MetaOutput.Vout)};

this.{nameof(MetaOutput.Type)} = ${nameof(MetaOutput.Type)};
this.{nameof(MetaOutput.Satoshis)} = 0;
this.{nameof(MetaOutput.Address)} = null;
this.{nameof(MetaOutput.TokenId)} = null;
this.{nameof(MetaOutput.Hash160)} = null;
this.{nameof(MetaOutput.ScriptPubKey)} = null;
this.{nameof(MetaOutput.Symbol)} = null;

this.{nameof(MetaOutput.InputIdx)} = 0;
this.{nameof(MetaOutput.SpendTxId)} = null;
this.{nameof(MetaOutput.Spent)} = false;

this['@metadata'] = {{ 
    '@collection': 'MetaOutputs', 
    'Raven-Clr-Type': '{typeof(MetaOutput).FullName}, {typeof(MetaOutput).Assembly.GetName().Name}' 
}};
";

    private static readonly string UpdateMetaOutputQuery = $@"
this.{nameof(MetaOutput.Type)} = ${nameof(MetaOutput.Type)};
this.{nameof(MetaOutput.Satoshis)} = ${nameof(MetaOutput.Satoshis)};
this.{nameof(MetaOutput.Address)} = ${nameof(MetaOutput.Address)};
this.{nameof(MetaOutput.TokenId)} = ${nameof(MetaOutput.TokenId)};
this.{nameof(MetaOutput.Hash160)} = ${nameof(MetaOutput.Hash160)};
this.{nameof(MetaOutput.ScriptPubKey)} = ${nameof(MetaOutput.ScriptPubKey)};
this.{nameof(MetaOutput.Symbol)} = ${nameof(MetaOutput.Symbol)};
";


    private static readonly string UpdateMetaInputQuery = $@"
this.{nameof(MetaOutput.InputIdx)} = ${nameof(MetaOutput.InputIdx)};
this.{nameof(MetaOutput.SpendTxId)} = ${nameof(MetaOutput.SpendTxId)};
this.{nameof(MetaOutput.Spent)} = true;
";

    private static readonly string InsertMetaOutputQuery = $@"
{InsertOutputQuery}

{UpdateMetaOutputQuery}
";

    private static readonly string InsertMetaInputQuery = $@"
{InsertOutputQuery}
    
{UpdateMetaInputQuery}
";

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
            };

            var patchOutput = new PatchOperation(
                output.Id,
                null,
                new PatchRequest
                {
                    Script = UpdateMetaOutputQuery,
                    Values = values
                },
                new PatchRequest
                {
                    Script = InsertMetaOutputQuery,
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
                    Script = UpdateMetaInputQuery,
                    Values = values
                },
                new PatchRequest
                {
                    Script = InsertMetaInputQuery,
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
                Script = UpdateStasAttributesQuery
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

    private const string FreeMetaOutputQuery = $@"
if (this.{nameof(MetaOutput.SpendTxId)} == ${nameof(MetaOutput.SpendTxId)}) {{
    this.{nameof(MetaOutput.InputIdx)} = 0;
    this.{nameof(MetaOutput.SpendTxId)} = null;
    this.{nameof(MetaOutput.Spent)} = false;
}}
";

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
                            Script = FreeMetaOutputQuery,
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
                    ? Transaction.Parse(rawData.Hex, Network.Mainnet)
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