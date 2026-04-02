using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Consigliere.BackgroundTasks.Realtime;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Infrastructure.Bitails.Realtime;

namespace Dxs.Consigliere.Tests.BackgroundTasks.Realtime;

public class BitailsRealtimeSubscriptionScopeProviderTests
{
    [Fact]
    public async Task UsesAddressTopicsWhenOnlyAddressesAreTracked()
    {
        var store = new FakeTransactionStore(
            addresses: [new Address("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa")],
            tokens: []);
        var provider = CreateProvider(store, new ConsigliereSourcesConfig
        {
            Providers =
            {
                Bitails =
                {
                    Connection =
                    {
                        Transport = Dxs.Consigliere.Configs.BitailsRealtimeTransportMode.Websocket,
                        Websocket = new BitailsWebsocketConnectionConfig
                        {
                            BaseUrl = "https://test-api.bitails.io/global"
                        }
                    }
                }
            }
        });

        var scope = await provider.BuildAsync();

        Assert.False(scope.UsesGlobalTransactions);
        Assert.Equal(Dxs.Infrastructure.Bitails.Realtime.BitailsRealtimeTransportMode.WebSocket, scope.TransportPlan.Mode);
        Assert.Equal("https://test-api.bitails.io/global", scope.TransportPlan.Endpoint.ToString());
        Assert.Equal([
            "lock-address-1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
            "spent-address-1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa"
        ], scope.TransportPlan.Topics.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task FallsBackToGlobalTransactionsWhenTrackedTokensExist()
    {
        var store = new FakeTransactionStore(
            addresses: [new Address("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa")],
            tokens: [TokenId.Parse("89abcdefabbaabbaabbaabbaabbaabbaabbaabba", Network.Mainnet)]);
        var provider = CreateProvider(store, new ConsigliereSourcesConfig
        {
            Providers =
            {
                Bitails =
                {
                    Connection =
                    {
                        Transport = Dxs.Consigliere.Configs.BitailsRealtimeTransportMode.Websocket
                    }
                }
            }
        });

        var scope = await provider.BuildAsync();

        Assert.True(scope.UsesGlobalTransactions);
        Assert.Equal(
        [
            "lock-address-1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
            "spent-address-1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
            "tx"
        ], scope.TransportPlan.Topics);
        Assert.Equal(1, scope.TokenCount);
    }

    [Fact]
    public async Task UsesZmqPlanWhenBitailsTransportIsConfiguredAsZmq()
    {
        var store = new FakeTransactionStore(
            addresses: [new Address("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa")],
            tokens: []);
        var provider = CreateProvider(store, new ConsigliereSourcesConfig
        {
            Providers =
            {
                Bitails =
                {
                    Connection =
                    {
                        Transport = Dxs.Consigliere.Configs.BitailsRealtimeTransportMode.Zmq,
                        Zmq = new BitailsZmqConnectionConfig
                        {
                            TxUrl = "tcp://bitails.example:28332"
                        }
                    }
                }
            }
        });

        var scope = await provider.BuildAsync();

        Assert.Equal(Dxs.Infrastructure.Bitails.Realtime.BitailsRealtimeTransportMode.Zmq, scope.TransportPlan.Mode);
        Assert.Equal("tcp://bitails.example:28332/", scope.TransportPlan.Endpoint.ToString());
        Assert.Equal(
        [
            "rawtx2",
            "removedfrommempoolblock",
            "discardedfrommempool",
            "hashblock2"
        ],
        scope.TransportPlan.Topics);
    }

    private static BitailsRealtimeSubscriptionScopeProvider CreateProvider(FakeTransactionStore store, ConsigliereSourcesConfig config)
        => new(store, new BitailsRealtimeTransportPlanner(), new FakeAdminRuntimeSourcePolicyService(config));

    private sealed class FakeTransactionStore(
        IReadOnlyCollection<Address> addresses,
        IReadOnlyCollection<TokenId> tokens
    ) : ITransactionStore
    {
        public Task<List<Address>> GetWatchingAddresses() => Task.FromResult(addresses.ToList());
        public Task<List<TokenId>> GetWatchingTokens() => Task.FromResult(tokens.ToList());

        public Task<TransactionProcessStatus> SaveTransaction(
            Transaction transaction,
            long timestamp,
            string firstOutToRedeem,
            string blockHash = null,
            int? blockHeight = null,
            int? indexInBlock = null
        ) => throw new NotSupportedException();

        public Task<Transaction> TryRemoveTransaction(string id) => throw new NotSupportedException();
    }

    private sealed class FakeAdminRuntimeSourcePolicyService(ConsigliereSourcesConfig config)
        : IAdminRuntimeSourcePolicyService
    {
        public Task<ConsigliereSourcesConfig> GetEffectiveSourcesConfigAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(config);

        public Task<AdminRuntimeSourcesResponse> GetRuntimeSourcesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdminRealtimeSourcePolicyMutationResult> ApplyRealtimePolicyAsync(
            string primaryRealtimeSource,
            string bitailsTransport,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdminRuntimeSourcesResponse> ResetRealtimePolicyAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
