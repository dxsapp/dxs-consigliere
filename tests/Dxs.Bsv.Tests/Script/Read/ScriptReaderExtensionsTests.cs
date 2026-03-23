using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.Tests.Script.Read;

public class ScriptReaderExtensionsTests
{
    [Fact]
    public void GetTokenIdForDstasUsesRedemptionHash160()
    {
        var owner = RepeatHex("11", 20);
        var redemption = RepeatHex("22", 20);
        var scriptHex = string.Concat(
            BuildPush(owner),
            "00",
            RepeatOpcode("76", 14),
            "6a",
            BuildPush(redemption),
            "00"
        );

        var reader = LockingScriptReader.Read(scriptHex, Network.Mainnet);

        Assert.Equal(ScriptType.DSTAS, reader.ScriptType);
        Assert.Equal(redemption, reader.GetTokenId());
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
}
