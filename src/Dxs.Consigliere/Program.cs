using Dxs.Consigliere;

using Microsoft.OpenApi.Writers;
using Serilog;
using Swashbuckle.AspNetCore.Swagger;

var environmentName =
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environments.Development;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

// wave-A2 S1: `--emit-swagger <path>` writes the v1 OpenAPI doc to
// disk and exits 0. Lets the admin-ui contracts pipeline regen
// types from the canonical Swashbuckle graph without booting
// background tasks or the DB-migration runner. We treat the emit
// as a build-time affordance (run via `pnpm contracts:generate`)
// rather than a runtime endpoint.
var emitSwaggerIndex = Array.IndexOf(args, "--emit-swagger");
if (emitSwaggerIndex >= 0)
{
    if (emitSwaggerIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("--emit-swagger requires a path argument");
        return 2;
    }
    var emitPath = args[emitSwaggerIndex + 1];
    try
    {
        var emitBuilder = Host
            .CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(c => c.AddJsonFile($"appsettings.{environmentName}.json", true))
            .ConfigureWebHostDefaults(w => w.UseStartup<Startup>())
            .UseEnvironment(environmentName);
        var emitApp = emitBuilder.Build();
        var provider = emitApp.Services.GetRequiredService<ISwaggerProvider>();
        var document = provider.GetSwagger("v1");
        var dir = Path.GetDirectoryName(Path.GetFullPath(emitPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await using var fileStream = File.Create(emitPath);
        await using var streamWriter = new StreamWriter(fileStream);
        var jsonWriter = new OpenApiJsonWriter(streamWriter);
        document.SerializeAsV3(jsonWriter);
        Log.Information("Wrote OpenAPI document to {Path}", emitPath);
        return 0;
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed to emit swagger document");
        return 1;
    }
}

Log.Information("Starting up {Environment}", environmentName);

try
{
    var builder = Host
        .CreateDefaultBuilder(args)
        .UseSystemd()
        // wave-A2 S2: `CreateDefaultBuilder` already loads
        // `appsettings.json` + `appsettings.{env}.json` followed by
        // environment variables + command-line args. We add the
        // env-specific JSON again BEFORE re-applying env vars +
        // cmdline so they remain authoritative — otherwise
        // `RavenDb__Urls__0=...` got silently overridden by
        // appsettings.{env}.json reading its own values back in.
        .ConfigureAppConfiguration(configBuilder =>
            {
                configBuilder.AddJsonFile($"appsettings.{environmentName}.json", true);
                configBuilder.AddEnvironmentVariables();
                configBuilder.AddCommandLine(args);
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
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
    return 1;
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
