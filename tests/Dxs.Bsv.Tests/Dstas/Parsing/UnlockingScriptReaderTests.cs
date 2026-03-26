using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.Tests.Script.Read;

public class UnlockingScriptReaderTests
{
    [Fact]
    public void ParsesDstasSpendingTypeFromTailTokens()
    {
        var preimage = RepeatHex("aa", 160);
        var signature = RepeatHex("30", 70);
        var publicKey = "02" + RepeatHex("11", 32);
        var unlockingHex = string.Concat(
            BuildPush("abcd"),
            BuildPush(preimage),
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
        var preimage = RepeatHex("aa", 160);
        var shortSignature = "3044";
        var publicKey = "02" + RepeatHex("11", 32);
        var unlockingHex = string.Concat(
            BuildPush("abcd"),
            BuildPush(preimage),
            "54",
            BuildPush(shortSignature),
            BuildPush(publicKey)
        );

        var reader = UnlockingScriptReader.Read(unlockingHex, Network.Mainnet);

        Assert.Null(reader.DstasSpendingType);
    }

    [Fact]
    public void ParsesDstasSpendingTypeFromAuthorityMultisigTailTokens()
    {
        var preimage = RepeatHex("aa", 160);
        var signatureA = RepeatHex("30", 72);
        var signatureB = RepeatHex("31", 71);
        var authorityPreimage = "03" + RepeatHex("22", 39);

        var unlockingHex = string.Concat(
            BuildPush("abcd"),
            BuildPush(preimage),
            "52",
            "00",
            BuildPush(signatureA),
            BuildPush(signatureB),
            BuildPush(authorityPreimage)
        );

        var reader = UnlockingScriptReader.Read(unlockingHex, Network.Mainnet);

        Assert.Equal(2, reader.DstasSpendingType);
    }

    [Fact]
    public void ParsesDstasSwapCancelFromSimpleTailTokens()
    {
        var preimage = RepeatHex("bb", 160);
        var signature = RepeatHex("30", 71);
        var publicKey = "03" + RepeatHex("12", 32);
        var unlockingHex = string.Concat(
            BuildPush("beef"),
            BuildPush(preimage),
            "54",
            BuildPush(signature),
            BuildPush(publicKey)
        );

        var reader = UnlockingScriptReader.Read(unlockingHex, Network.Mainnet);

        Assert.Equal(4, reader.DstasSpendingType);
    }

    [Fact]
    public void ParsesDstasConfiscationFromAuthorityMultisigTailTokens()
    {
        var preimage = RepeatHex("cc", 160);
        var signatureA = RepeatHex("30", 72);
        var signatureB = RepeatHex("31", 70);
        var authorityPreimage = "03" + RepeatHex("44", 39);

        var unlockingHex = string.Concat(
            BuildPush("cafe"),
            BuildPush(preimage),
            "53",
            "00",
            BuildPush(signatureA),
            BuildPush(signatureB),
            BuildPush(authorityPreimage)
        );

        var reader = UnlockingScriptReader.Read(unlockingHex, Network.Mainnet);

        Assert.Equal(3, reader.DstasSpendingType);
    }

    [Fact]
    public void DoesNotParseAuthorityMultisigTailWhenDummyOpcodeIsMissing()
    {
        var preimage = RepeatHex("aa", 160);
        var signature = RepeatHex("30", 72);
        var authorityPreimage = "02" + "21" + "02" + RepeatHex("33", 32) + "02";

        var unlockingHex = string.Concat(
            BuildPush(preimage),
            "53",
            BuildPush(signature),
            BuildPush(authorityPreimage)
        );

        var reader = UnlockingScriptReader.Read(unlockingHex, Network.Mainnet);

        Assert.Null(reader.DstasSpendingType);
    }

    private static string BuildPush(string dataHex)
    {
        var byteLength = dataHex.Length / 2;
        if (byteLength < 0x4c)
            return $"{byteLength:x2}{dataHex}";

        if (byteLength <= byte.MaxValue)
            return $"4c{byteLength:x2}{dataHex}";

        throw new ArgumentOutOfRangeException(nameof(dataHex), "Test helper only supports push lengths up to OP_PUSHDATA1.");
    }

    private static string RepeatHex(string byteHex, int count)
        => string.Concat(Enumerable.Repeat(byteHex, count));
}
