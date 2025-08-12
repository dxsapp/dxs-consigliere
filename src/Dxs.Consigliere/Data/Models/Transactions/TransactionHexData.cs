using Dxs.Common.Extensions;

namespace Dxs.Consigliere.Data.Models.Transactions;

public class TransactionHexData
{
    public string Id { get; set; }
    public string TxId { get; set; }
    public string Hex { get; set; }

    public static string GetId(string txId) => $"tx/raw/{txId}";

    public static string Parse(string str)
    {
        if (str.IsNullOrEmpty())
            throw new ArgumentException("Required", nameof(str));

        var segments = str.Split("/");

        return segments.Length != 3
            ? str
            : segments[2];
    }
}