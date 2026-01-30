using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Common.Extensions;

public static class ServiceExtensions
{
    public static IDisposable GetScopedService<T>(this IServiceProvider provider, out T service, bool required = true)
    {
        var scope = provider.CreateScope();
        service = required ? scope.ServiceProvider.GetRequiredService<T>() : scope.ServiceProvider.GetService<T>();
        return scope;
    }

    public static IDisposable GetScopedServices<T1, T2>(this IServiceProvider provider, out T1 service1, out T2 service2, bool required = true)
    {
        var scope = provider.CreateScope();
        service1 = required ? scope.ServiceProvider.GetRequiredService<T1>() : scope.ServiceProvider.GetService<T1>();
        service2 = required ? scope.ServiceProvider.GetRequiredService<T2>() : scope.ServiceProvider.GetService<T2>();
        return scope;
    }

    public static IDisposable GetScopedServices<T1, T2, T3>(this IServiceProvider provider, out T1 service1, out T2 service2, out T3 service3,
        bool required = true)
    {
        var scope = provider.CreateScope();
        service1 = required ? scope.ServiceProvider.GetRequiredService<T1>() : scope.ServiceProvider.GetService<T1>();
        service2 = required ? scope.ServiceProvider.GetRequiredService<T2>() : scope.ServiceProvider.GetService<T2>();
        service3 = required ? scope.ServiceProvider.GetRequiredService<T3>() : scope.ServiceProvider.GetService<T3>();
        return scope;
    }

    public static IDisposable GetScopedServices<T1, T2, T3, T4>(
        this IServiceProvider provider,
        out T1 service1,
        out T2 service2,
        out T3 service3,
        out T4 service4,
        bool required = true)
    {
        var scope = provider.CreateScope();
        service1 = required ? scope.ServiceProvider.GetRequiredService<T1>() : scope.ServiceProvider.GetService<T1>();
        service2 = required ? scope.ServiceProvider.GetRequiredService<T2>() : scope.ServiceProvider.GetService<T2>();
        service3 = required ? scope.ServiceProvider.GetRequiredService<T3>() : scope.ServiceProvider.GetService<T3>();
        service4 = required ? scope.ServiceProvider.GetRequiredService<T4>() : scope.ServiceProvider.GetService<T4>();
        return scope;
    }

    public static T GetOptionsValue<T>(this IServiceProvider provider) where T : class =>
        provider.GetRequiredService<IOptions<T>>().Value;

    public static T CreateInstance<T>(this IServiceProvider provider, params object[] parameters) =>
        ActivatorUtilities.CreateInstance<T>(provider, parameters);

    public static OptionsBuilder<T> AddOptions<T>(this IServiceCollection provider, IConfiguration config) where T : class =>
        provider.AddOptions<T>().Bind(config);

    public static T Bind<T>(this IConfiguration configuration) where T : new()
    {
        var instance = new T();
        configuration.Bind(instance, options => options.BindNonPublicProperties = true);
        return instance;
    }

    public static void GetRequiredServices<T1, T2>(this IServiceProvider provider, out T1 service1, out T2 service2)
    {
        service1 = provider.GetRequiredService<T1>();
        service2 = provider.GetRequiredService<T2>();
    }
}
