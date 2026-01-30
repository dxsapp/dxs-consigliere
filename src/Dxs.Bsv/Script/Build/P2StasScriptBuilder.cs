using Dxs.Bsv.Tokens;

namespace Dxs.Bsv.Script.Build;

public class P2StasScriptBuilder : ScriptBuilder
{
    public P2StasScriptBuilder(Address toAddress, ITokenSchema schema) : this(toAddress, schema.TokenId, schema.Symbol) { }

    public P2StasScriptBuilder(Address toAddress, string tokenId, string symbol) : base(ScriptType.P2STAS, toAddress)
    {
        foreach (var token in ScriptSamples.StasV3Tokens)
        {
            if (token.IsReceiverId)
            {
                Tokens.Add(new(toAddress.Hash160)
                {
                    IsReceiverId = true
                });
            }
            else
            {
                Tokens.Add(token.Clone());
            }
        }

        AddOpCode(OpCode.OP_RETURN);
        AddData(tokenId.FromHexString());

        if (!string.IsNullOrEmpty(symbol))
            AddData(symbol);
    }
}
