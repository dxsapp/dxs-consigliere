using System;
using Dxs.Bsv.Factories.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Bsv.Factories;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// IUtxoSetProvider is required in DI
    /// </summary>
    public static IServiceCollection AddTransactionFactories(this IServiceCollection services)
        => services
            .AddOptions<UtxoCache.CacheDuration>().Configure(cache =>
            {
                cache.Broadcasted = TimeSpan.FromHours(4);
                cache.Enumerated = TimeSpan.FromSeconds(30);
            }).Services
            .AddSingleton<IUtxoCache, UtxoCache>()
            .AddSingleton<IP2PkhTransactionFactory, P2PkhTransactionFactory>()
            .AddSingleton<IStasBundleTransactionFactory, StasBundleTransactionFactory>();
}