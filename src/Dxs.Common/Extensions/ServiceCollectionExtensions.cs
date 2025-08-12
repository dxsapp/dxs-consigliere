using Dxs.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TrustMargin.Common.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOptions{TOptions}"/> and <typeparamref name="TOptions"/> to the services container.
    /// Also runs data annotation validation.
    /// </summary>
    /// <typeparam name="TOptions">The type of the options.</typeparam>
    /// <param name="services">The services collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The same services collection.</returns>
    public static IServiceCollection ConfigureAndValidateSingleton<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TOptions: class, new()
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        services
            .AddOptions<TOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations();

        return services.AddSingleton(x => x.GetRequiredService<IOptions<TOptions>>().Value);
    }

    public static IServiceCollection AddSingletonHostedService<T, TImpl>(this IServiceCollection services)
        where TImpl: class, T, IBackgroundTask, IHostedService
        where T: class
        => services
            .AddSingletonHostedService<TImpl>()
            .AddSingleton<T>(p => p.GetRequiredService<TImpl>());

    public static IServiceCollection AddSingletonHostedService<T1, T2, TImpl>(this IServiceCollection services)
        where TImpl: class, T1, T2, IBackgroundTask, IHostedService
        where T1: class
        where T2: class
        => services
            .AddSingletonHostedService<TImpl>()
            .AddSingleton<T1>(p => p.GetRequiredService<TImpl>())
            .AddSingleton<T2>(p => p.GetRequiredService<TImpl>());

    public static IServiceCollection AddSingletonHostedService<T>(this IServiceCollection services)
        where T: class, IBackgroundTask, IHostedService
        => services
            .AddSingleton<T>()
            .AddHostedService(p => p.GetRequiredService<T>());
}