using System.Text.Json;

using Dxs.Bsv;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Tests.Data.Models.Transactions;

public class MetaOutputDstasMappingTests
{
    [Fact]
    public void MapsDstasProtocolFieldsFromOutputReader()
    {
        var vector = LoadVector("transfer_regular_valid");
        var transaction = Transaction.Parse(vector.TxHex, Network.Mainnet);
        var output = transaction.Outputs.First(x => x.Type == ScriptType.DSTAS);
        var outPoint = new OutPoint(transaction, output.Idx);
        var reader = LockingScriptReader.Read(outPoint.ScriptPubKey, Network.Mainnet);

        var metaOutput = MetaOutput.FromOutput(transaction, output, timestamp: 0, height: 0);
        var dstas = reader.Dstas!;
        var serviceFields = dstas.ServiceFields.Select(x => x.ToHexString()).ToArray();
        var optionalFields = dstas.OptionalData.Select(x => x.ToHexString()).ToArray();

        Assert.Equal(ScriptType.DSTAS, metaOutput.Type);
        Assert.Equal(output.TokenId, metaOutput.TokenId);
        Assert.Equal(dstas.Flags.ToHexString(), metaOutput.DstasFlags);
        Assert.Equal(dstas.FreezeEnabled, metaOutput.DstasFreezeEnabled);
        Assert.Equal(dstas.ConfiscationEnabled, metaOutput.DstasConfiscationEnabled);
        Assert.Equal(dstas.Frozen, metaOutput.DstasFrozen);
        Assert.Equal(dstas.ActionType, metaOutput.DstasActionType);

        var expectedActionData = dstas.ActionDataRaw.Length > 0 ? dstas.ActionDataRaw.ToHexString() : null;
        Assert.Equal(expectedActionData, metaOutput.DstasActionData);
        Assert.Equal(dstas.RequestedScriptHash?.ToHexString(), metaOutput.DstasRequestedScriptHash);

        Assert.Equal(serviceFields, metaOutput.DstasServiceFields);
        Assert.Equal(optionalFields, metaOutput.DstasOptionalData);
        Assert.Equal(optionalFields.Length > 0 ? string.Join("|", optionalFields) : null, metaOutput.DstasOptionalDataFingerprint);
    }

    [Fact]
    public void MapsServiceAuthoritiesUsingFlagsOrder()
    {
        var vector = LoadVector("confiscate_valid");
        var transaction = Transaction.Parse(vector.TxHex, Network.Mainnet);
        var output = transaction.Outputs.First(x => x.Type == ScriptType.DSTAS);

        var metaOutput = MetaOutput.FromOutput(transaction, output, timestamp: 0, height: 0);

        if (metaOutput.DstasFreezeEnabled == true && metaOutput.DstasConfiscationEnabled == true)
        {
            Assert.NotNull(metaOutput.DstasServiceFields);
            Assert.True(metaOutput.DstasServiceFields!.Length >= 2);
            Assert.Equal(metaOutput.DstasServiceFields[0], metaOutput.DstasFreezeAuthority);
            Assert.Equal(metaOutput.DstasServiceFields[1], metaOutput.DstasConfiscationAuthority);
        }
        else if (metaOutput.DstasFreezeEnabled == true)
        {
            Assert.NotNull(metaOutput.DstasFreezeAuthority);
            Assert.Null(metaOutput.DstasConfiscationAuthority);
        }
        else if (metaOutput.DstasConfiscationEnabled == true)
        {
            Assert.Null(metaOutput.DstasFreezeAuthority);
            Assert.NotNull(metaOutput.DstasConfiscationAuthority);
        }
    }

    [Fact]
    public void MapsSwapRequestedScriptHashAndNeutralActionData()
    {
        var vector = LoadVector("swap_cancel_valid");
        var transaction = Transaction.Parse(vector.TxHex, Network.Mainnet);
        var output = transaction.Outputs.First(x => x.Type == ScriptType.DSTAS);
        var reader = LockingScriptReader.Read(new OutPoint(transaction, output.Idx).ScriptPubKey, Network.Mainnet);

        var metaOutput = MetaOutput.FromOutput(transaction, output, timestamp: 0, height: 0);

        Assert.NotNull(reader.Dstas);
        Assert.Equal(reader.Dstas.ActionType, metaOutput.DstasActionType);
        Assert.Equal(reader.Dstas!.RequestedScriptHash?.ToHexString(), metaOutput.DstasRequestedScriptHash);
        Assert.Equal(reader.Dstas.ActionDataRaw.Length > 0 ? reader.Dstas.ActionDataRaw.ToHexString() : null, metaOutput.DstasActionData);
    }

    private static ConformanceVector LoadVector(string id)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "dstas-conformance-vectors.json");
        var json = File.ReadAllText(fixturePath);
        var vectors = JsonSerializer.Deserialize<List<ConformanceVector>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(vectors);
        var vector = vectors!.FirstOrDefault(x => x.Id == id);
        Assert.NotNull(vector);
        return vector!;
    }

    private sealed class ConformanceVector
    {
        public string Id { get; set; } = string.Empty;
        public string TxHex { get; set; } = string.Empty;
    }
}
