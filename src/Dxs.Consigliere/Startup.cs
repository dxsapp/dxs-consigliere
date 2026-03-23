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
            .AddPublicApiZoneServices()
            .AddCorePlatformZoneServices(configuration)
            .AddExternalChainAdapterZoneServices()
            .AddIndexerStateZoneServices()
            .AddRealtimeZoneServices()
            .AddIndexerOrchestrationZoneServices()
            .AddHostedTaskZoneServices();
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
