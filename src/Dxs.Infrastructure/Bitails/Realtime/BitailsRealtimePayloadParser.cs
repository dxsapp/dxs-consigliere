using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dxs.Infrastructure.Bitails.Realtime;

public static class BitailsRealtimePayloadParser
{
    private static readonly Regex TxIdRegex = new("^[0-9a-fA-F]{64}$", RegexOptions.Compiled);

    public static string ExtractTxId(string value)
        => TryExtractTxId(value, out var txId) ? txId : null;

    public static string ExtractTxId(JsonElement element)
        => TryExtractTxId(element, out var txId) ? txId : null;

    public static bool TryExtractTxId(string value, out string txId)
    {
        txId = null;
        if (!IsTxId(value))
            return false;

        txId = value!.ToLowerInvariant();
        return true;
    }

    public static bool TryExtractTxId(JsonElement element, out string txId)
    {
        txId = null;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return TryExtractTxId(element.GetString(), out txId);
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    if ((property.NameEquals("txid") || property.NameEquals("txId") || property.NameEquals("transactionId") || property.NameEquals("id")) &&
                        TryExtractTxId(property.Value, out txId))
                    {
                        return true;
                    }

                    if (TryExtractTxId(property.Value, out txId))
                        return true;
                }

                return false;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (TryExtractTxId(item, out txId))
                        return true;
                }

                return false;
            }
            default:
                return false;
        }
    }

    private static bool IsTxId(string value)
        => !string.IsNullOrWhiteSpace(value) && TxIdRegex.IsMatch(value);
}
