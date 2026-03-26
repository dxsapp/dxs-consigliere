using Dxs.Bsv;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Tests.Dstas.Persistence;

public class StasDerivedTransactionStateEvaluatorTests
{
    private readonly StasDerivedTransactionStateEvaluator _sut = new();

    [Fact]
    public void Evaluate_MapsSwapCancelFromKnownParent()
    {
        var transaction = new MetaTransaction
        {
            Id = "child",
            Inputs =
            [
                new MetaTransaction.Input
                {
                    TxId = "parent",
                    Vout = 0,
                    DstasSpendingType = 4
                }
            ],
            Outputs =
            [
                new MetaTransaction.Output
                {
                    Type = ScriptType.DSTAS,
                    Address = "1Holder",
                    TokenId = "token-1",
                    Hash160 = "token-1"
                }
            ]
        };

        var parents = new Dictionary<string, MetaTransaction>
        {
            ["parent"] = new()
            {
                Id = "parent",
                Outputs =
                [
                    new MetaTransaction.Output
                    {
                        Type = ScriptType.DSTAS,
                        Address = "1Issuer",
                        TokenId = "token-1",
                        Hash160 = "issuer-hash"
                    }
                ],
                IllegalRoots = [],
                MissingTransactions = []
            }
        };

        var state = _sut.Evaluate(transaction, parents);

        Assert.True(state.IsStas);
        Assert.Equal(4, state.DstasSpendingType);
        Assert.Equal("swap_cancel", state.DstasEventType);
        Assert.Equal(TokenProjectionProtocolType.Dstas, state.ProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Valid, state.ValidationStatus);
        Assert.True(state.CanProjectTokenOutputs);
    }

    [Fact]
    public void Evaluate_TracksMissingDependenciesFromAbsentParents()
    {
        var transaction = new MetaTransaction
        {
            Id = "child",
            Inputs =
            [
                new MetaTransaction.Input
                {
                    TxId = "missing-parent",
                    Vout = 0
                }
            ],
            Outputs =
            [
                new MetaTransaction.Output
                {
                    Type = ScriptType.DSTAS,
                    Address = "1Holder",
                    TokenId = "token-1"
                }
            ]
        };

        var state = _sut.Evaluate(transaction, new Dictionary<string, MetaTransaction>());

        Assert.True(state.IsStas);
        Assert.False(state.AllStasInputsKnown);
        Assert.Contains("missing-parent", state.MissingTransactions);
        Assert.Equal(TokenProjectionValidationStatus.Invalid, state.ValidationStatus);
        Assert.False(state.CanProjectTokenOutputs);
    }

    [Fact]
    public void Evaluate_ReplaysOwnerMultisigPositiveSpendFixtureWithoutUnknownState()
    {
        var chain = DstasProtocolOwnerFixture.LoadChain("owner_multisig_positive_spend");
        var parentStates = new Dictionary<string, MetaTransaction>(StringComparer.Ordinal);

        foreach (var fixture in chain.Transactions)
        {
            foreach (var group in fixture.Prevouts.GroupBy(x => x.TxId, StringComparer.Ordinal))
            {
                if (parentStates.ContainsKey(group.Key))
                    continue;

                parentStates[group.Key] = BuildSeedParent(group.Key, group);
            }

            var transaction = BuildMetaTransaction(Transaction.Parse(fixture.TxHex, Network.Mainnet));
            var state = _sut.Evaluate(transaction, parentStates);

            transaction.IsStas = state.IsStas;
            transaction.IsIssue = state.IsIssue;
            transaction.IsValidIssue = state.IsValidIssue;
            transaction.IsRedeem = state.IsRedeem;
            transaction.IsWithFee = state.IsWithFee;
            transaction.IsWithNote = state.IsWithNote;
            transaction.AllStasInputsKnown = state.AllStasInputsKnown;
            transaction.RedeemAddress = state.RedeemAddress;
            transaction.StasFrom = state.StasFrom;
            transaction.DstasEventType = state.DstasEventType;
            transaction.DstasSpendingType = state.DstasSpendingType;
            transaction.DstasInputFrozen = state.DstasInputFrozen;
            transaction.DstasOutputFrozen = state.DstasOutputFrozen;
            transaction.DstasOptionalDataContinuity = state.DstasOptionalDataContinuity;
            transaction.TokenIds = state.TokenIds.ToList();
            transaction.IllegalRoots = state.IllegalRoots.ToList();
            transaction.MissingTransactions = state.MissingTransactions.ToList();
            transaction.StasProtocolType = state.ProtocolType;
            transaction.StasValidationStatus = state.ValidationStatus;
            transaction.CanProjectTokenOutputs = state.CanProjectTokenOutputs;

            parentStates[transaction.Id] = transaction;
        }

        var issue = parentStates.Values.Single(x => x.IsIssue);
        var spend = parentStates[Transaction.Parse(chain.Transactions.Single(x => x.Label == "owner_multisig_spend").TxHex, Network.Mainnet).Id];

        Assert.True(issue.IsValidIssue);
        Assert.Equal(TokenProjectionProtocolType.Dstas, issue.StasProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Valid, issue.StasValidationStatus);
        Assert.True(spend.AllStasInputsKnown);
        Assert.Empty(spend.MissingTransactions);
        Assert.Equal(TokenProjectionValidationStatus.Valid, spend.StasValidationStatus);
        Assert.True(spend.CanProjectTokenOutputs);
    }

    private static MetaTransaction BuildMetaTransaction(Transaction transaction)
    {
        var outputs = transaction.Outputs
            .Select(output => new MetaTransaction.Output(MetaOutput.FromOutput(transaction, output, 0, MetaTransaction.DefaultHeight)))
            .ToList();

        return new MetaTransaction
        {
            Id = transaction.Id,
            Inputs = transaction.Inputs
                .Select((input, idx) => new MetaTransaction.Input(MetaOutput.FromInput(transaction, input, idx, 0, MetaTransaction.DefaultHeight))
                {
                    DstasSpendingType = input.DstasSpendingType
                })
                .ToList(),
            Outputs = outputs,
            Addresses = outputs.Where(x => x.Address is not null).Select(x => x.Address!).Distinct(StringComparer.Ordinal).ToList(),
            TokenIds = outputs.Where(x => x.TokenId is not null).Select(x => x.TokenId!).Distinct(StringComparer.Ordinal).ToList(),
            IllegalRoots = [],
            MissingTransactions = []
        };
    }

    private static MetaTransaction BuildSeedParent(string txId, IEnumerable<DstasProtocolPrevoutFixture> prevouts)
    {
        var prevoutsByVout = prevouts.ToDictionary(x => x.Vout);
        var maxVout = prevoutsByVout.Keys.Max();
        var outputs = new List<MetaTransaction.Output>(maxVout + 1);

        for (var vout = 0; vout <= maxVout; vout++)
        {
            outputs.Add(prevoutsByVout.TryGetValue(vout, out var prevout)
                ? BuildSeedOutput(prevout)
                : new MetaTransaction.Output
                {
                    Id = MetaOutput.GetId(txId, vout),
                    Type = ScriptType.Unknown,
                    Satoshis = 0
                });
        }

        return new MetaTransaction
        {
            Id = txId,
            Inputs = [],
            Outputs = outputs,
            Addresses = outputs.Where(x => x.Address is not null).Select(x => x.Address!).Distinct(StringComparer.Ordinal).ToList(),
            TokenIds = outputs.Where(x => x.TokenId is not null).Select(x => x.TokenId!).Distinct(StringComparer.Ordinal).ToList(),
            IsStas = outputs.Any(x => x.Type is ScriptType.P2STAS or ScriptType.DSTAS),
            IllegalRoots = [],
            MissingTransactions = [],
            AllStasInputsKnown = true
        };
    }

    private static MetaTransaction.Output BuildSeedOutput(DstasProtocolPrevoutFixture prevout)
    {
        var reader = LockingScriptReader.Read(prevout.LockingScriptHex, Network.Mainnet);
        var dstas = reader.Dstas;
        var serviceFields = dstas?.ServiceFields.Select(x => x.ToHexString()).ToArray();
        var optionalData = dstas?.OptionalData.Select(x => x.ToHexString()).ToArray();

        return new MetaTransaction.Output
        {
            Id = MetaOutput.GetId(prevout.TxId, prevout.Vout),
            Type = reader.ScriptType,
            Satoshis = prevout.Satoshis,
            Address = reader.Address?.Value,
            TokenId = reader.GetTokenId(),
            Hash160 = reader.Address?.Hash160.ToHexString(),
            DstasFlags = dstas?.Flags?.ToHexString(),
            DstasFreezeEnabled = dstas?.FreezeEnabled,
            DstasConfiscationEnabled = dstas?.ConfiscationEnabled,
            DstasFrozen = dstas?.Frozen,
            DstasFreezeAuthority = serviceFields != null && dstas?.FreezeEnabled == true && serviceFields.Length > 0 ? serviceFields[0] : null,
            DstasConfiscationAuthority = serviceFields != null && dstas?.ConfiscationEnabled == true
                ? serviceFields[dstas.FreezeEnabled ? 1 : 0]
                : null,
            DstasServiceFields = serviceFields,
            DstasActionType = dstas?.ActionType,
            DstasActionData = dstas?.ActionDataRaw is { Length: > 0 } ? dstas.ActionDataRaw.ToHexString() : null,
            DstasRequestedScriptHash = dstas?.RequestedScriptHash?.ToHexString(),
            DstasOptionalData = optionalData,
            DstasOptionalDataFingerprint = optionalData is { Length: > 0 } ? string.Join("|", optionalData) : null
        };
    }
}
