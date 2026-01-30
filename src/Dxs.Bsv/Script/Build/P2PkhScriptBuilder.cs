namespace Dxs.Bsv.Script.Build;

public class P2PkhBuilderScript : ScriptBuilder
{
    private bool _isOpReturnAdded;

    public P2PkhBuilderScript(Address toAddress) : base(ScriptType.P2PKH, toAddress)
    {
        foreach (var token in ScriptSamples.P2PhkTokens)
        {
            if (token.IsReceiverId)
            {
                Tokens.Add(new(ToAddress.Hash160)
                {
                    IsReceiverId = true
                });
            }
            else
            {
                Tokens.Add(token.Clone());
            }
        }
    }

    public void AddReturnData(byte[] data)
    {
        if (!_isOpReturnAdded)
        {
            AddOpCode(OpCode.OP_RETURN);
            _isOpReturnAdded = true;
        }

        AddData(data);
    }
}
