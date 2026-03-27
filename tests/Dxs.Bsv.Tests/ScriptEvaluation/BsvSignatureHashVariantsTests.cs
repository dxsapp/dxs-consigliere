using Dxs.Bsv.Models;
using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Build;
using Dxs.Bsv.ScriptEvaluation;
using Dxs.Bsv.Transactions.Build;

namespace Dxs.Bsv.Tests.ScriptEvaluation;

public class BsvSignatureHashVariantsTests
{
    [Theory]
    [InlineData(SignatureHashType.SIGHASH_ALL | SignatureHashType.SIGHASH_FORKID)]
    [InlineData(SignatureHashType.SIGHASH_NONE | SignatureHashType.SIGHASH_FORKID)]
    [InlineData(SignatureHashType.SIGHASH_SINGLE | SignatureHashType.SIGHASH_FORKID)]
    [InlineData(SignatureHashType.SIGHASH_ALL | SignatureHashType.SIGHASH_FORKID | SignatureHashType.SIGHASH_ANYONECANPAY)]
    [InlineData(SignatureHashType.SIGHASH_NONE | SignatureHashType.SIGHASH_FORKID | SignatureHashType.SIGHASH_ANYONECANPAY)]
    [InlineData(SignatureHashType.SIGHASH_SINGLE | SignatureHashType.SIGHASH_FORKID | SignatureHashType.SIGHASH_ANYONECANPAY)]
    public void NativeInterpreter_AcceptsForkIdSighashVariants(SignatureHashType sighashType)
    {
        var signer = new PrivateKey(Network.Mainnet);
        var recipientA = new PrivateKey(Network.Mainnet).P2PkhAddress;
        var recipientB = new PrivateKey(Network.Mainnet).P2PkhAddress;
        var change = new PrivateKey(Network.Mainnet).P2PkhAddress;

        var prevouts = new[]
        {
            new OutPoint(new string('1', 64), signer.P2PkhAddress, string.Empty, 9_000, 0),
            new OutPoint(new string('2', 64), signer.P2PkhAddress, string.Empty, 7_000, 1)
        };

        var builder = TransactionBuilder.Init()
            .AddInput(prevouts[0], signer)
            .AddInput(prevouts[1], signer)
            .AddP2PkhOutput(2_000, recipientA)
            .AddP2PkhOutput(3_000, recipientB)
            .AddP2PkhOutput(10_000, change);

        foreach (var input in builder.Inputs)
            input.UnlockingScript = BuildP2PkhUnlockingScript(input, signer, sighashType);

        var tx = builder.BuildTransaction(Network.Mainnet);
        var result = new TransactionEvaluationService().EvaluateTransaction(tx, new DictionaryPrevoutResolver(prevouts));

        Assert.True(result.Success, string.Join(",", result.Inputs.Where(x => !x.Success).Select(x => $"{x.InputIndex}:{x.ErrorCode}")));
    }

    [Fact]
    public void NativeInterpreter_RejectsNonForkIdSighash()
    {
        var signer = new PrivateKey(Network.Mainnet);
        var recipient = new PrivateKey(Network.Mainnet).P2PkhAddress;
        var prevout = new OutPoint(new string('3', 64), signer.P2PkhAddress, string.Empty, 5_000, 0);

        var builder = TransactionBuilder.Init()
            .AddInput(prevout, signer)
            .AddP2PkhOutput(4_000, recipient);

        builder.Inputs[0].UnlockingScript = BuildP2PkhUnlockingScript(builder.Inputs[0], signer, SignatureHashType.SIGHASH_ALL);

        var tx = builder.BuildTransaction(Network.Mainnet);
        var result = new TransactionEvaluationService().EvaluateTransaction(tx, new DictionaryPrevoutResolver([prevout]));

        Assert.False(result.Success);
        Assert.Equal("EvalFalse", result.Inputs.Single().ErrorCode);
    }

    private static byte[] BuildP2PkhUnlockingScript(BaseInputBuilder input, PrivateKey signer, SignatureHashType sighashType)
    {
        var preimage = input.Preimage(sighashType);
        var sighash = Hash.Sha256Sha256(preimage.Bytes);
        var der = signer.Sign(sighash);
        var derWithSigHash = new byte[der.Length + 1];

        Buffer.BlockCopy(der, 0, derWithSigHash, 0, der.Length);
        derWithSigHash[^1] = (byte)sighashType;

        return new ScriptBuilder(ScriptType.P2PKH, null)
            .AddData(derWithSigHash)
            .AddData(signer.PublicKey)
            .Bytes;
    }
}
