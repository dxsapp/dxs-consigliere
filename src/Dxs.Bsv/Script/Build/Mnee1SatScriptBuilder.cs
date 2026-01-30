using Dxs.Common.Extensions;

namespace Dxs.Bsv.Script.Build;

public class Mnee1SatScriptBuilder : ScriptBuilder
{
    public const int DataIdx = 6;
    public const int ApproverIdx = 13;

    private const string TokenInfoTemplate = """{"p":"bsv-20","op":"{OPERATION}","id":"{TOKEN_ID}","amt":"{AMOUNT}"}""";

    public Mnee1SatScriptBuilder(
        Address toAddress,
        string approverPubKey,
        string tokenData
    ) : base(ScriptType.Mnee1Sat, toAddress)
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
            else if (i == ApproverIdx)
            {
                Tokens.Add(new(approverPubKey.FromHexString()));
            }
            else if (i == DataIdx)
            {
                Tokens.Add(new(tokenData.ToUtf8Bytes()));
            }
            else
            {
                Tokens.Add(token.Clone());
            }
        }
    }

    public static string BuildTransferTokenInfo(string tokenId, long amount)
        => TokenInfoTemplate
            .Replace("{OPERATION}", "transfer")
            .Replace("{TOKEN_ID}", tokenId)
            .Replace("{AMOUNT}", amount.ToString());
}
