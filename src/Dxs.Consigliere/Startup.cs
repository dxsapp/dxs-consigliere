using System.Text.Json.Serialization;

using Dxs.Bsv;
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
using Dxs.Common.Cache;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Notifications;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Consigliere.Setup;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.JungleBus;
using Dxs.Infrastructure.WoC;

using MediatR;

using Microsoft.OpenApi.Models;

using Raven.Migrations;

using TrustMargin.Common.Extensions;

namespace Dxs.Consigliere;

public class Startup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Add data base dependencies
        services
            .Configure<RavenDbConfig>(configuration.GetSection("RavenDb"))
            .AddSingleton<RavenDbDocumentStore>()
            .AddRavenDbMigrations(options =>
            {
                options.PreventSimultaneousMigrations = true;
                options.SimultaneousMigrationTimeout = TimeSpan.FromMinutes(30);
            })
            .AddSingleton(sp => sp.GetRequiredService<RavenDbDocumentStore>().DocumentStore)
            .AddScoped(sp => sp.GetRequiredService<RavenDbDocumentStore>().DocumentStore.GetSession());

        // add bsv dependencies
        services
            .Configure<ZmqClientConfig>(configuration.GetSection("ZmqClient"))
            .Configure<RpcConfig>(configuration.GetSection("BsvNodeApi"))
            .Configure<TransactionFilterConfig>(configuration.GetSection("TransactionFilter"))
            .AddSingleton<ITransactionStore>(sp => sp.GetRequiredService<IMetaTransactionStore>())
            .AddSingleton<ITxMessageBus, TxMessageBus>()
            .AddSingleton<IBlockMessageBus, BlockMessageBus>()
            .AddSingleton<ITransactionFilter, TransactionFilter>()
            .AddSingleton<IZmqClient, ZmqClient>()
            .AddSingleton<IFilteredTransactionMessageBus, FilteredTransactionMessageBus>()
            .AddTransient<ITokenTransactionFactory, StasProtocolTransactionFactory>()
            .AddTransient<IRpcClient, RpcClient>()
            .AddHttpClient<IRpcClient, RpcClient>()
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(30)); // for reading blocks on a flight

        // Add Authentication and Authorization
        services
            .AddHttpContextAccessor()
            .AddSignalRForApp()
            .AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Services
            .AddResponseCompression(x => { x.EnableForHttps = true; })
            .AddRequestDecompression()
            .AddEndpointsApiExplorer()
            .AddSwaggerGen()
            ;

        // Add self dependencies
        services
            .Configure<AppConfig>(configuration)
            .Configure<NetworkConfig>(configuration)
            .AddSingleton<INetworkProvider, NetworkProvider>()
            .AddTransient<IBitcoindService, BitcoindService>()
            .AddTransient<IBroadcastProvider>(sp => sp.GetRequiredService<IBitcoindService>())

            .AddTransient<IBitailsRestApiClient, BitailsRestApiClient>()
            .AddHttpClient<IBitailsRestApiClient, BitailsRestApiClient>().Services
            .AddSingleton<IWhatsOnChainRestApiClient, WhatsOnChainRestApiClient>()
            .AddHttpClient<IWhatsOnChainRestApiClient, WhatsOnChainRestApiClient>().Services

            .AddTransient<IMetaTransactionStore, TransactionStore>()
            .AddSingleton<ConnectionManager>()
            .AddSingleton<IConnectionManager>(sp => sp.GetRequiredService<ConnectionManager>())
            .AddSingleton<INotificationHandler<TransactionDeleted>>(sp => sp.GetRequiredService<ConnectionManager>())

            .AddSingleton<UtxoSetManager>()
            .AddSingleton<IUtxoSetProvider>(sp => sp.GetRequiredService<UtxoSetManager>())
            .AddSingleton<IUtxoManager>(sp => sp.GetRequiredService<UtxoSetManager>())
            .AddCache()
            .AddTransactionFactories()
            .AddSingleton<IBroadcastService, BroadcastService>()
            .AddSingleton<IAddressHistoryService, AddressHistoryService>()
            .AddTransient<JungleBusWebsocketClient>()
            .AddHttpClient<JungleBusWebsocketClient>()
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30)).Services
            .AddTransient<JungleBusBlockchainDataProvider>()
            .AddTransient<NodeBlockchainDataProvider>()
            ;

        // add background tasks
        services
            .AddSingletonHostedService<AppInitBackgroundTask>()
            .AddSingletonHostedService<BlockProcessBackgroundTask>()
            .AddSingletonHostedService<ActualChainTipVerifyBackgroundTask>()
            .AddSingletonHostedService<StasAttributesMissingTransactions>()
            .AddSingletonHostedService<StasAttributesChangeObserverTask>()
            .AddSingletonHostedService<JungleBusSyncMissingDataBackgroundTask>()
            .AddSingletonHostedService<UnconfirmedTransactionsMonitor>()
            .AddSingletonHostedService<JungleBusMempoolMonitor>()
            ;

        // others
        services.AddMediatR(cfg => { cfg.RegisterServicesFromAssemblyContaining<IMediator>(); });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseCors(x => x
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
        );

        app.UseStaticFiles();
        app.UseRouting();

        // if (env.IsProduction())
        // {
        //     //app.UseHttpsRedirection();
        //     app.UseHsts();
        // }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseResponseCompression();
        app.UseRequestDecompression();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller}/{action=Index}/{id?}");
        });
        app.UseSignalR();

        app.UseSwagger();
        app.UseSwaggerUI();
    }

    public static void InitializeDatabase(IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredService<MigrationRunner>().Run();
}
