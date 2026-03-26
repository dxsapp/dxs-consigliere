using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Impl;
using Dxs.Bsv.Factories;
using Dxs.Bsv.Rpc.Configs;
using Dxs.Bsv.Rpc.Services;
using Dxs.Bsv.Rpc.Services.Impl;
using Dxs.Bsv.Tokens;
using Dxs.Bsv.Tokens.Stas;
using Dxs.Bsv.Zmq;
using Dxs.Bsv.Zmq.Configs;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Setup;

public static class BsvRuntimeSetup
{
    public static IServiceCollection AddBsvRuntimeZoneServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
        => services
            .Configure<ZmqClientConfig>(configuration.GetSection("ZmqClient"))
            .Configure<RpcConfig>(configuration.GetSection("BsvNodeApi"))
            .Configure<TransactionFilterConfig>(configuration.GetSection("TransactionFilter"))
            .AddSingleton<ITransactionStore>(sp => sp.GetRequiredService<IMetaTransactionStore>())
            .AddSingleton<ITxObservationSink, JournalFirstTxObservationSink>()
            .AddSingleton<ITxMessageBus, TxMessageBus>()
            .AddSingleton<IBlockMessageBus, BlockMessageBus>()
            .AddSingleton<ITransactionFilter, TransactionFilter>()
            .AddSingleton<IZmqClient, ZmqClient>()
            .AddSingleton<IFilteredTransactionMessageBus, FilteredTransactionMessageBus>()
            .AddTransient<ITokenTransactionFactory, StasProtocolTransactionFactory>()
            .AddTransient<IRpcClient, RpcClient>()
            .AddHttpClient<IRpcClient, RpcClient>()
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(30)).Services;
}
