using Dxs.Consigliere;

using Serilog;

var environmentName =
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environments.Development;

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
            }
        )
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        })
        .UseEnvironment(environmentName);

    var app = builder.Build();

    await InitializeDatabaseWithRetryAsync(app.Services, environmentName);

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

static async Task InitializeDatabaseWithRetryAsync(IServiceProvider services, string environmentName)
{
    var maxAttempts = string.Equals(environmentName, "DockerComposeE2E", StringComparison.OrdinalIgnoreCase) ? 30 : 5;
    var delay = TimeSpan.FromSeconds(2);

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            Startup.InitializeDatabase(services);
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            Log.Warning(
                ex,
                "Database initialization attempt {Attempt}/{MaxAttempts} failed; retrying in {DelaySeconds}s",
                attempt,
                maxAttempts,
                delay.TotalSeconds
            );
            await Task.Delay(delay);
        }
    }

    Startup.InitializeDatabase(services);
}
