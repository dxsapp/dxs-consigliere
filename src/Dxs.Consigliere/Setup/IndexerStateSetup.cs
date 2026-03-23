using Dxs.Bsv.Factories;
using Dxs.Common.Cache;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;

using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Setup;

public static class IndexerStateSetup
{
    public static IServiceCollection AddIndexerStateZoneServices(this IServiceCollection services)
        => services
            .AddTransient<IMetaTransactionStore, TransactionStore>()
            .AddSingleton<UtxoSetManager>()
            .AddSingleton<IUtxoSetProvider>(sp => sp.GetRequiredService<UtxoSetManager>())
            .AddSingleton<IUtxoManager>(sp => sp.GetRequiredService<UtxoSetManager>())
            .AddCache()
            .AddTransactionFactories()
            .AddSingleton<IAddressHistoryService, AddressHistoryService>();
}
