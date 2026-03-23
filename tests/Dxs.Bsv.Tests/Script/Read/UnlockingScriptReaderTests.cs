using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.Tests.Script.Read;

public class UnlockingScriptReaderTests
{
    [Fact]
    public void ParsesDstasSpendingTypeFromTailTokens()
    {
        var signature = RepeatHex("30", 70);
        var publicKey = "02" + RepeatHex("11", 32);
        var unlockingHex = string.Concat(
            BuildPush("abcd"),
            "53",
            BuildPush(signature),
            BuildPush(publicKey)
        );

        var reader = UnlockingScriptReader.Read(unlockingHex, Network.Mainnet);

        Assert.Equal(3, reader.DstasSpendingType);
    }

    [Fact]
    public void DoesNotParseSpendingTypeWhenTailIsNotDstasLike()
    {
        var shortSignature = "3044";
        var publicKey = "02" + RepeatHex("11", 32);
        var unlockingHex = string.Concat(
            BuildPush("abcd"),
            "54",
            BuildPush(shortSignature),
            BuildPush(publicKey)
        );

        var reader = UnlockingScriptReader.Read(unlockingHex, Network.Mainnet);

        Assert.Null(reader.DstasSpendingType);
    }

    private static string BuildPush(string dataHex)
    {
        var byteLength = dataHex.Length / 2;
        if (byteLength >= 0x4c)
            throw new ArgumentOutOfRangeException(nameof(dataHex), "Test helper only supports push lengths below OP_PUSHDATA1.");

        return $"{byteLength:x2}{dataHex}";
    }

    private static string RepeatHex(string byteHex, int count)
        => string.Concat(Enumerable.Repeat(byteHex, count));
}
