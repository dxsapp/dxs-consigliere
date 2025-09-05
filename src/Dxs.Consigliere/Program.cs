using Dxs.Consigliere;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

var environmentName = Environment.GetEnvironmentVariable(HostDefaults.EnvironmentKey) ?? "Development";

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting up {Environment}", environmentName);

try
{
    var builder = Host
        .CreateDefaultBuilder(args)
        .UseSystemd()
        .ConfigureAppConfiguration(configBuilder =>
            {
                configBuilder.AddJsonFile($"appsettings.{environmentName}.json", true);
                configBuilder.AddJsonFile($"appsettings.TransactionFilter.json", true);
            }
        )
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        })
        .UseEnvironment(environmentName);

    var app = builder.Build();

    Startup.InitializeDatabase(app.Services);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}