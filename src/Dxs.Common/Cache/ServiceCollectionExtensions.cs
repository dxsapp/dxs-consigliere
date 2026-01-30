using Dxs.Common.Interfaces;

using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Common.Cache;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCache(this IServiceCollection services)
        => services
            // see https://github.com/khellang/Scrutor/issues/96#issuecomment-749173709
            .AddSingleton(typeof(IAppCache<>), typeof(LazyCacheAppCache<>))
        ;
}
