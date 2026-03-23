using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Build;
using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.Tests.Script.Read;

public class P2MpkhDetectionTests
{
    [Fact]
    public void DetectsP2MpkhFromScriptSampleTokens()
    {
        var scriptBytes = BuildScriptBytes(ScriptSamples.P2MpkhTokens);

        var reader = LockingScriptReader.Read(scriptBytes, Network.Mainnet);

        Assert.Equal(ScriptType.P2MPKH, reader.ScriptType);
        Assert.NotNull(reader.Address);
    }

    [Fact]
    public void P2MpkhAndDstasUseP2PkhAddressPrefix()
    {
        var hash160 = "00112233445566778899aabbccddeeff00112233";
        var p2pkh = BitcoinHelpers.GetAddressFromHash160(hash160, ScriptType.P2PKH, Network.Mainnet);
        var p2mpkh = BitcoinHelpers.GetAddressFromHash160(hash160, ScriptType.P2MPKH, Network.Mainnet);
        var dstas = BitcoinHelpers.GetAddressFromHash160(hash160, ScriptType.DSTAS, Network.Mainnet);

        Assert.Equal(p2pkh, p2mpkh);
        Assert.Equal(p2pkh, dstas);
    }

    private static byte[] BuildScriptBytes(IReadOnlyList<ScriptBuildToken> tokens)
    {
        var size = tokens.Sum(ScriptBuilder.GetScriptTokenSize);
        var writer = new BufferWriter(size);

        foreach (var token in tokens)
        {
            var data = token.Bytes ?? new byte[token.DataLength];
            var scriptToken = new ScriptReadToken(token.OpCodeNum, data);
            writer.WriteScriptToken(scriptToken);
        }

        return writer.Bytes;
    }
}
