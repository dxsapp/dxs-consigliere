namespace Dxs.Bsv.Script.Build;

public class Mnee1SatScriptBuilder: ScriptBuilder
{
    public const int DataIdx = 6;
    public const int ApproverIds = 13;
    
    public Mnee1SatScriptBuilder(Address toAddress, string approverPk): base(ScriptType.Mnee1Sat, toAddress)
    {
        for (var i = 0; i < ScriptSamples.Mnee1SatTokens.Count; i++)
        {
            var token = ScriptSamples.Mnee1SatTokens[i];
            if (token.IsReceiverId)
            {
                Tokens.Add(new(toAddress.Hash160)
                {
                    IsReceiverId = true
                });
            }
            else if (i == ApproverIds)
            {
                Tokens.Add(new(approverPk.FromHexString()));
            }
            else
            {
                Tokens.Add(token.Clone());
            }
        }
    }
}