using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.Tests.Script.Read;

public class LockingScriptReaderOnePassTests
{
    [Fact]
    public void ReadsDstasFlagsAndServiceAuthoritiesInOnePass()
    {
        var owner = RepeatHex("11", 20);
        var redemption = RepeatHex("22", 20);
        var freezeAuthority = RepeatHex("33", 20);
        var confiscationAuthority = RepeatHex("44", 20);

        var scriptHex = BuildDstasScript(
            ownerHex: owner,
            secondFieldTokenHex: "52",
            redemptionHex: redemption,
            flagsTokenHex: BuildPush("03"),
            serviceFieldHexes:
            [
                freezeAuthority,
                confiscationAuthority
            ],
            optionalFieldHexes:
            [
                "aabb"
            ]
        );

        var reader = LockingScriptReader.Read(scriptHex, Network.Mainnet);

        Assert.Equal(ScriptType.DSTAS, reader.ScriptType);
        Assert.NotNull(reader.Dstas);
        Assert.Equal(owner, ToLowerHex(reader.Dstas!.Owner));
        Assert.Equal(redemption, ToLowerHex(reader.Dstas.Redemption));
        Assert.Equal("03", ToLowerHex(reader.Dstas.Flags));
        Assert.True(reader.Dstas.FreezeEnabled);
        Assert.True(reader.Dstas.ConfiscationEnabled);
        Assert.True(reader.Dstas.Frozen);
        Assert.Equal("empty", reader.Dstas.ActionType);
        Assert.Empty(reader.Dstas.ActionDataRaw);
        Assert.Null(reader.Dstas.RequestedScriptHash);

        Assert.Equal(2, reader.Dstas.ServiceFields.Count);
        Assert.Equal(freezeAuthority, ToLowerHex(reader.Dstas.ServiceFields[0]));
        Assert.Equal(confiscationAuthority, ToLowerHex(reader.Dstas.ServiceFields[1]));
        Assert.Single(reader.Dstas.OptionalData);
        Assert.Equal("aabb", ToLowerHex(reader.Dstas.OptionalData[0]));
    }

    [Fact]
    public void ReadsSwapRequestedScriptHashFromSecondField()
    {
        var owner = RepeatHex("01", 20);
        var redemption = RepeatHex("02", 20);
        var requestedScriptHash = RepeatHex("ab", 32);
        var actionData = "01" + requestedScriptHash;

        var scriptHex = BuildDstasScript(
            ownerHex: owner,
            secondFieldTokenHex: BuildPush(actionData),
            redemptionHex: redemption,
            flagsTokenHex: "00",
            serviceFieldHexes: [],
            optionalFieldHexes:
            [
                "beef"
            ]
        );

        var reader = LockingScriptReader.Read(scriptHex, Network.Mainnet);

        Assert.Equal(ScriptType.DSTAS, reader.ScriptType);
        Assert.NotNull(reader.Dstas);
        Assert.False(reader.Dstas!.Frozen);
        Assert.False(reader.Dstas.FreezeEnabled);
        Assert.False(reader.Dstas.ConfiscationEnabled);
        Assert.Equal("swap", reader.Dstas.ActionType);
        Assert.Equal(actionData, ToLowerHex(reader.Dstas.ActionDataRaw));
        Assert.Equal(requestedScriptHash, ToLowerHex(reader.Dstas.RequestedScriptHash!));
        Assert.Empty(reader.Dstas.ServiceFields);
        Assert.Single(reader.Dstas.OptionalData);
        Assert.Equal("beef", ToLowerHex(reader.Dstas.OptionalData[0]));
    }

    private static string BuildDstasScript(
        string ownerHex,
        string secondFieldTokenHex,
        string redemptionHex,
        string flagsTokenHex,
        IReadOnlyList<string> serviceFieldHexes,
        IReadOnlyList<string> optionalFieldHexes
    )
    {
        var scriptHex = string.Concat(
            BuildPush(ownerHex),
            secondFieldTokenHex,
            RepeatOpcode("76", 14),
            "6a",
            BuildPush(redemptionHex),
            flagsTokenHex
        );

        foreach (var field in serviceFieldHexes)
            scriptHex += BuildPush(field);

        foreach (var field in optionalFieldHexes)
            scriptHex += BuildPush(field);

        return scriptHex;
    }

    private static string BuildPush(string dataHex)
    {
        var byteLength = dataHex.Length / 2;
        if (byteLength >= 0x4c)
            throw new ArgumentOutOfRangeException(nameof(dataHex), "Test helper only supports push lengths below OP_PUSHDATA1.");

        return $"{byteLength:x2}{dataHex}";
    }

    private static string RepeatOpcode(string opCodeHex, int count)
        => string.Concat(Enumerable.Repeat(opCodeHex, count));

    private static string RepeatHex(string byteHex, int count)
        => string.Concat(Enumerable.Repeat(byteHex, count));

    private static string ToLowerHex(byte[] bytes)
        => Convert.ToHexString(bytes).ToLowerInvariant();
}
