using Dxs.Consigliere.Data;
using Dxs.Consigliere.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Raven.Migrations;

namespace Dxs.Consigliere.Setup;

public static class PersistenceSetup
{
    public static IServiceCollection AddPersistenceZoneServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
        => services
            .Configure<RavenDbConfig>(configuration.GetSection("RavenDb"))
            .AddSingleton<RavenDbDocumentStore>()
            .AddRavenDbMigrations(options =>
            {
                options.PreventSimultaneousMigrations = true;
                options.SimultaneousMigrationTimeout = TimeSpan.FromMinutes(30);
            })
            .AddSingleton(sp => sp.GetRequiredService<RavenDbDocumentStore>().DocumentStore)
            .AddScoped(sp => sp.GetRequiredService<RavenDbDocumentStore>().DocumentStore.GetSession());
}
