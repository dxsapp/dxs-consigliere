using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Infrastructure.Bitails.Realtime;

namespace Dxs.Consigliere.BackgroundTasks.Realtime;

public sealed class BitailsRealtimeSubscriptionScopeProvider(
    ITransactionStore transactionStore,
    IBitailsRealtimeTransportPlanner transportPlanner,
    IAdminRuntimeSourcePolicyService runtimeSourcePolicyService
) : IBitailsRealtimeSubscriptionScopeProvider
{
    public async Task<BitailsRealtimeSubscriptionScope> BuildAsync(CancellationToken cancellationToken = default)
    {
        var addresses = (await transactionStore.GetWatchingAddresses())
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var tokens = await transactionStore.GetWatchingTokens();
        var usesGlobalTransactions = tokens.Count > 0;

        var targets = new List<BitailsRealtimeSubscriptionTarget>();

        foreach (var address in addresses)
        {
            targets.Add(new BitailsRealtimeSubscriptionTarget.AddressLock(address));
            targets.Add(new BitailsRealtimeSubscriptionTarget.AddressSpent(address));
        }

        if (usesGlobalTransactions)
            targets.Add(new BitailsRealtimeSubscriptionTarget.AllTransactions());

        var effectiveSources = await runtimeSourcePolicyService.GetEffectiveSourcesConfigAsync(cancellationToken);
        var transportPlan = BuildTransportPlan(effectiveSources.Providers.Bitails, targets);
        var signature = $"{transportPlan.Mode}:{transportPlan.Endpoint}|{string.Join('|', transportPlan.Topics)}";

        return new BitailsRealtimeSubscriptionScope(
            signature,
            transportPlan,
            addresses.Length,
            tokens.Count,
            usesGlobalTransactions
        );
    }

    private BitailsRealtimeTransportPlan BuildTransportPlan(
        BitailsSourceConfig config,
        IReadOnlyCollection<BitailsRealtimeSubscriptionTarget> targets
    )
    {
        var transport = config.Connection.Transport?.Trim().ToLowerInvariant();
        if (string.Equals(transport, Dxs.Consigliere.Configs.BitailsRealtimeTransportMode.Zmq, StringComparison.OrdinalIgnoreCase))
        {
            var raw = config.Connection.Zmq.TxUrl;
            if (string.IsNullOrWhiteSpace(raw))
                raw = config.Connection.Zmq.BlockUrl;

            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("Bitails realtime ZMQ transport requires txUrl or blockUrl.");

            return transportPlanner.CreateZmqPlan(new Uri(raw, UriKind.Absolute), targets.ToArray());
        }

        var endpoint = ResolveWebSocketEndpoint(config);
        return transportPlanner.CreateWebSocketPlan(endpoint, targets.ToArray());
    }

    private static Uri ResolveWebSocketEndpoint(BitailsSourceConfig config)
    {
        var raw = config.Connection.Websocket.BaseUrl;
        if (string.IsNullOrWhiteSpace(raw))
            raw = config.Connection.BaseUrl;
        if (string.IsNullOrWhiteSpace(raw))
            raw = BitailsRealtimeTopicCatalog.DefaultWebSocketEndpoint.ToString();

        return new Uri(raw, UriKind.Absolute);
    }
}
