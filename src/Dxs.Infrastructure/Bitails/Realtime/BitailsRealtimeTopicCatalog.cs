using System;
using System.Collections.Generic;
using System.Linq;

namespace Dxs.Infrastructure.Bitails.Realtime;

public static class BitailsRealtimeTopicCatalog
{
    public static Uri DefaultWebSocketEndpoint { get; } = new("https://api.bitails.io/global", UriKind.Absolute);

    public static IReadOnlyList<string> GetTopics(IEnumerable<BitailsRealtimeSubscriptionTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        return targets
            .SelectMany(GetTopics)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> GetTopics(BitailsRealtimeSubscriptionTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target switch
        {
            BitailsRealtimeSubscriptionTarget.AllTransactions => ["tx"],
            BitailsRealtimeSubscriptionTarget.AllAddresses => ["lock-address", "spent-address"],
            BitailsRealtimeSubscriptionTarget.AllScripthashes => ["lock-scripthash", "spent-scripthash"],
            BitailsRealtimeSubscriptionTarget.Transaction transaction => [BuildPrefixedTopic("tx", RequireValue(transaction.TxId, nameof(transaction.TxId)))],
            BitailsRealtimeSubscriptionTarget.AddressLock address => [BuildPrefixedTopic("lock-address", RequireValue(address.Address, nameof(address.Address)))],
            BitailsRealtimeSubscriptionTarget.AddressSpent address => [BuildPrefixedTopic("spent-address", RequireValue(address.Address, nameof(address.Address)))],
            BitailsRealtimeSubscriptionTarget.ScripthashLock scripthash => [BuildPrefixedTopic("lock-scripthash", RequireValue(scripthash.Scripthash, nameof(scripthash.Scripthash)))],
            BitailsRealtimeSubscriptionTarget.ScripthashSpent scripthash => [BuildPrefixedTopic("spent-scripthash", RequireValue(scripthash.Scripthash, nameof(scripthash.Scripthash)))],
            BitailsRealtimeSubscriptionTarget.UtxoSpent utxo => [BuildPrefixedTopic("utxo-spent", $"{RequireValue(utxo.TxId, nameof(utxo.TxId))}_{RequireNonNegative(utxo.OutputIndex, nameof(utxo.OutputIndex))}")],
            _ => throw new ArgumentOutOfRangeException(nameof(target), target.GetType().FullName, "Unsupported Bitails realtime subscription target.")
        };
    }

    private static string BuildPrefixedTopic(string prefix, string value) => $"{prefix}-{value}";

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Bitails realtime target requires {parameterName}.", parameterName);

        return value.Trim();
    }

    private static int RequireNonNegative(int value, string parameterName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameterName, value, "Bitails realtime target output index must be greater than or equal to zero.");

        return value;
    }
}
