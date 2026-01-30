using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Extensions;

public static class ServiceExtensions
{
    public static IDisposable GetScopedService<T>(this IServiceProvider provider, out T service, bool required)
    {
        var scope = provider.CreateScope();

        service = required
            ? scope.ServiceProvider.GetRequiredService<T>()
            : scope.ServiceProvider.GetService<T>();

        return scope;
    }

    public static IDisposable GetScopedService<T>(this IServiceProvider provider, out T service) =>
        provider.GetScopedService(out service, required: true);

    public static T GetOptionsValue<T>(this IServiceProvider provider) where T : class =>
        provider.GetRequiredService<IOptions<T>>().Value;

    public static T CreateInstance<T>(this IServiceProvider provider, params object[] parameters) =>
        ActivatorUtilities.CreateInstance<T>(provider, parameters);

    public static OptionsBuilder<T> AddOptions<T>(this IServiceCollection provider, IConfiguration config) where T : class =>
        provider.AddOptions<T>().Bind(config);
}
