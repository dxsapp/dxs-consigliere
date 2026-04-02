using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dxs.Infrastructure.Bitails.Realtime;

public static class BitailsRealtimePayloadParser
{
    private static readonly Regex TxIdRegex = new("^[0-9a-fA-F]{64}$", RegexOptions.Compiled);
    private static readonly Regex HexRegex = new("^[0-9a-fA-F]+$", RegexOptions.Compiled);

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

    public static bool TryExtractRawHex(string value, out string hex)
    {
        hex = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var candidate = value.Trim();
        if (!IsRawHex(candidate))
            return false;

        hex = candidate.ToLowerInvariant();
        return true;
    }

    public static bool TryExtractRawHex(JsonElement element, out string hex)
    {
        hex = null;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return TryExtractRawHex(element.GetString(), out hex);
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    if ((property.NameEquals("rawtx") || property.NameEquals("rawTx") || property.NameEquals("raw") ||
                         property.NameEquals("hex") || property.NameEquals("transactionHex") || property.NameEquals("txhex") ||
                         property.NameEquals("payload") || property.NameEquals("body")) &&
                        TryExtractRawHex(property.Value, out hex))
                    {
                        return true;
                    }

                    if (TryExtractRawHex(property.Value, out hex))
                        return true;
                }

                return false;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (TryExtractRawHex(item, out hex))
                        return true;
                }

                return false;
            }
            default:
                return false;
        }
    }

    public static bool TryExtractBlockHash(string value, out string blockHash)
        => TryExtractTxId(value, out blockHash);

    public static bool TryExtractBlockHash(JsonElement element, out string blockHash)
    {
        blockHash = null;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return TryExtractBlockHash(element.GetString(), out blockHash);
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    if ((property.NameEquals("blockHash") || property.NameEquals("blockhash") || property.NameEquals("hash")) &&
                        TryExtractBlockHash(property.Value, out blockHash))
                    {
                        return true;
                    }

                    if (TryExtractBlockHash(property.Value, out blockHash))
                        return true;
                }

                return false;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (TryExtractBlockHash(item, out blockHash))
                        return true;
                }

                return false;
            }
            default:
                return false;
        }
    }

    public static bool TryExtractRemoveReason(JsonElement element, out string reason)
    {
        reason = null;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in element.EnumerateObject())
        {
            if ((property.NameEquals("reason") || property.NameEquals("removeReason")) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                reason = property.Value.GetString();
                return !string.IsNullOrWhiteSpace(reason);
            }
        }

        return false;
    }

    public static bool TryExtractCollidedWithTransaction(JsonElement element, out string txId)
    {
        txId = null;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in element.EnumerateObject())
        {
            if ((property.NameEquals("collidedWith") || property.NameEquals("collidedWithTransaction")) &&
                TryExtractTxId(property.Value, out txId))
            {
                return true;
            }

            if ((property.NameEquals("collidedWithTxId") || property.NameEquals("collidedWithTxid")) &&
                property.Value.ValueKind == JsonValueKind.String &&
                TryExtractTxId(property.Value.GetString(), out txId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTxId(string value)
        => !string.IsNullOrWhiteSpace(value) && TxIdRegex.IsMatch(value);

    private static bool IsRawHex(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Length > 64 &&
           value.Length % 2 == 0 &&
           HexRegex.IsMatch(value);
}
