using Dxs.Consigliere.Setup;
using Raven.Migrations;

namespace Dxs.Consigliere;

public class Startup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
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
        app.UseResponseCompression();
        app.UseRequestDecompression();

        app.UseEndpoints(endpoints =>
        {
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
        => serviceProvider.GetRequiredService<MigrationRunner>().Run();
}
