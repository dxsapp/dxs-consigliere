using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Setup;
using Raven.Client.Documents;
using Raven.Migrations;

namespace Dxs.Consigliere;

public class Startup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddConsigliereForwardedHeaders(configuration)
            .AddConsigliereHealthChecks()
            .AddConsigliereRateLimiting(configuration)
            .AddPersistenceZoneServices(configuration)
            .AddBsvRuntimeZoneServices(configuration)
            .AddBsvP2pZoneServices(configuration)
            .AddPublicApiZoneServices()
            .AddCorePlatformZoneServices(configuration)
            .AddExternalChainAdapterZoneServices()
            .AddIndexerStateZoneServices()
            .AddRealtimeZoneServices()
            .AddIndexerOrchestrationZoneServices()
            .AddHostedTaskZoneServices()
            .AddMetricsZoneServices(configuration);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // wave-A3 S0: MUST run before UseCors/UseRouting so
        // downstream middleware sees the original scheme + client
        // IP from the X-Forwarded-* headers Caddy injects.
        app.UseForwardedHeaders();

        app.UseCors(x => x
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
        );

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();

        // if (env.IsProduction())
        // {
        //     //app.UseHttpsRedirection();
        //     app.UseHsts();
        // }

        app.UseAuthentication();
        app.UseAuthorization();
        // wave-A3 S1: must come AFTER auth so the limiter sees
        // resolved identity (future per-user partitions) but
        // BEFORE the endpoint dispatcher so [EnableRateLimiting]
        // attributes actually fire. Health endpoints registered
        // below opt out via DisableRateLimiting.
        app.UseRateLimiter();
        app.UseResponseCompression();
        app.UseRequestDecompression();

        app.UseEndpoints(endpoints =>
        {
            // wave-A3 S2: anonymous health probes. Registered
            // before controller routes so the fallback-to-index
            // map doesn't shadow them.
            endpoints.MapConsigliereHealthEndpoints();

            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller}/{action=Index}/{id?}");
            endpoints.MapFallbackToFile("index.html");
        });
        app.UseSignalR();

        // wave-A2 S1: the Swagger UI is a dev/CI affordance. Keep
        // the OpenAPI middleware itself available in every env (the
        // admin UI's contract-parity test runs against the document
        // in CI), but expose the Swagger UI only when the host is
        // not Production. The `--emit-swagger <path>` CLI flag in
        // Program.cs bypasses both — it serialises the doc from the
        // already-built DI graph and exits.
        app.UseSwagger();
        if (!env.IsProduction())
        {
            app.UseSwaggerUI();
        }
    }

    public static void InitializeDatabase(IServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<MigrationRunner>().Run();

        // wave-A3 S5: one-shot migration of the wave-A2 Raven
        // provider-config document onto the on-disk secrets
        // store. Fail-stop — if the file write or Raven delete
        // throws, the host refuses to start so the operator
        // fixes the underlying issue instead of running with
        // half-migrated state.
        var fileStore = serviceProvider.GetRequiredService<SecretsFileStore>();
        var documentStore = serviceProvider.GetRequiredService<IDocumentStore>();
        fileStore.MigrateFromRavenAsync(documentStore).GetAwaiter().GetResult();
    }
}
