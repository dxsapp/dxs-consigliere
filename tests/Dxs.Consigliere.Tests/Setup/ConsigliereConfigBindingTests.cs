using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Setup;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Setup;

public class ConsigliereConfigBindingTests
{
    [Fact]
    public void AddCorePlatformZoneServices_BindsSourcesStorageCacheAndAdminAuthOptions()
    {
        var values = new Dictionary<string, string?>
        {
            ["Network"] = "Mainnet",
            ["Consigliere:Sources:Routing:PreferredMode"] = "hybrid",
            ["Consigliere:Sources:Routing:PrimarySource"] = "junglebus",
            ["Consigliere:Sources:Routing:FallbackSources:0"] = "bitails",
            ["Consigliere:Sources:Routing:VerificationSource"] = "node",
            ["Consigliere:Sources:Capabilities:Broadcast:Mode"] = "multi",
            ["Consigliere:Sources:Capabilities:Broadcast:Sources:0"] = "node",
            ["Consigliere:Storage:RawTransactionPayloads:Enabled"] = "true",
            ["Consigliere:Storage:RawTransactionPayloads:Provider"] = "raven",
            ["Consigliere:Storage:RawTransactionPayloads:Location:Collection"] = "RawTransactionPayloads",
            ["Consigliere:Storage:RawTransactionPayloads:Compression:Enabled"] = "true",
            ["Consigliere:Storage:RawTransactionPayloads:Compression:Algorithm"] = "zstd",
            ["Consigliere:Cache:Enabled"] = "true",
            ["Consigliere:Cache:Backend"] = "memory",
            ["Consigliere:Cache:MaxEntries"] = "2048",
            ["Consigliere:Cache:SafetyTtlSeconds"] = "45",
            ["Consigliere:AdminAuth:Enabled"] = "true",
            ["Consigliere:AdminAuth:Username"] = "operator",
            ["Consigliere:AdminAuth:PasswordHash"] = "pbkdf2-sha256$100000$MTIzNDU2Nzg5MGFiY2RlZg==$YnJmW6O0dN0Y6iT7d2hM4GgK4D6s6H6fN0j8gP3hNlg=",
            ["Consigliere:AdminAuth:SessionTtlMinutes"] = "90",
            ["Consigliere:AdminAuth:CookieName"] = "consigliere_admin",
            ["VNextRuntime:CutoverMode"] = "shadow_read"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorePlatformZoneServices(configuration);

        using var provider = services.BuildServiceProvider();

        var sources = provider.GetRequiredService<IOptions<ConsigliereSourcesConfig>>().Value;
        var storage = provider.GetRequiredService<IOptions<ConsigliereStorageConfig>>().Value;
        var cache = provider.GetRequiredService<IOptions<ConsigliereCacheConfig>>().Value;
        var adminAuth = provider.GetRequiredService<IOptions<ConsigliereAdminAuthConfig>>().Value;
        var appConfig = provider.GetRequiredService<IOptions<AppConfig>>().Value;

        Assert.Equal("hybrid", sources.Routing.PreferredMode);
        Assert.Equal("junglebus", sources.Routing.PrimarySource);
        Assert.Equal("node", sources.Routing.VerificationSource);
        Assert.Equal(["junglebus", "node", "bitails"], sources.Routing.FallbackSources);

        Assert.True(sources.Providers.Node.Enabled);
        Assert.Contains("broadcast", sources.Providers.Node.EnabledCapabilities);
        Assert.True(sources.Providers.Bitails.Enabled);
        Assert.Equal("websocket", sources.Providers.Bitails.Connection.Transport);
        Assert.Equal("https://api.bitails.io/global", sources.Providers.Bitails.Connection.Websocket.BaseUrl);
        Assert.Equal("https://api.whatsonchain.com/v1/bsv/main", sources.Providers.Whatsonchain.Connection.BaseUrl);

        Assert.Equal("multi", sources.Capabilities.Broadcast.Mode);
        Assert.Contains("node", sources.Capabilities.Broadcast.Sources);
        Assert.Contains("bitails", sources.Capabilities.Broadcast.Sources);

        Assert.True(storage.RawTransactionPayloads.Enabled);
        Assert.Equal("raven", storage.RawTransactionPayloads.Provider);
        Assert.Equal("RawTransactionPayloads", storage.RawTransactionPayloads.Location.Collection);
        Assert.NotNull(storage.RawTransactionPayloads.Compression);
        Assert.True(storage.RawTransactionPayloads.Compression.Enabled);
        Assert.Equal("zstd", storage.RawTransactionPayloads.Compression.Algorithm);
        Assert.True(cache.Enabled);
        Assert.Equal("memory", cache.Backend);
        Assert.Equal(2048, cache.MaxEntries);
        Assert.Equal(45, cache.SafetyTtlSeconds);
        Assert.True(adminAuth.Enabled);
        Assert.Equal("operator", adminAuth.Username);
        Assert.Equal(90, adminAuth.SessionTtlMinutes);
        Assert.Equal("consigliere_admin", adminAuth.CookieName);
        Assert.Equal("shadow_read", appConfig.VNextRuntime.CutoverMode);
    }

    [Fact]
    public void AddCorePlatformZoneServices_RejectsEnabledAdminAuthWithoutCredentials()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Network"] = "Mainnet",
                ["Consigliere:AdminAuth:Enabled"] = "true",
                ["Consigliere:AdminAuth:Username"] = "operator"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorePlatformZoneServices(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ConsigliereAdminAuthConfig>>().Value);

        Assert.Contains("PasswordHash", exception.Failures.Single());
    }

    [Fact]
    public void AddCorePlatformZoneServices_RejectsUnknownSourceProviderReferences()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Network"] = "Mainnet",
                ["Consigliere:Sources:Routing:PrimarySource"] = "mystery-provider"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorePlatformZoneServices(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ConsigliereSourcesConfig>>().Value);

        Assert.Contains("Unknown provider", exception.Failures.Single());
    }

    [Fact]
    public void AddCorePlatformZoneServices_RejectsUnknownPreferredMode()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Network"] = "Mainnet",
                ["Consigliere:Sources:Routing:PreferredMode"] = "mesh",
                ["Consigliere:Sources:Routing:PrimarySource"] = "node"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorePlatformZoneServices(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ConsigliereSourcesConfig>>().Value);

        Assert.Contains("Unknown preferred mode", exception.Failures.Single());
    }

    [Fact]
    public void AddCorePlatformZoneServices_IgnoresProviderOverridesFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Network"] = "Mainnet",
                ["Consigliere:Sources:Providers:Bitails:Enabled"] = "false",
                ["Consigliere:Sources:Providers:Bitails:Connection:Transport"] = "zmq",
                ["Consigliere:Sources:Providers:Bitails:Connection:Zmq:TxUrl"] = "tcp://127.0.0.1:28332"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorePlatformZoneServices(configuration);

        using var provider = services.BuildServiceProvider();
        var sources = provider.GetRequiredService<IOptions<ConsigliereSourcesConfig>>().Value;

        Assert.True(sources.Providers.Bitails.Enabled);
        Assert.Equal("websocket", sources.Providers.Bitails.Connection.Transport);
        Assert.Equal("https://api.bitails.io/global", sources.Providers.Bitails.Connection.Websocket.BaseUrl);
    }

    [Fact]
    public void AddCorePlatformZoneServices_RejectsEnabledStorageWithoutRequiredLocation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Network"] = "Mainnet",
                ["Consigliere:Storage:RawTransactionPayloads:Enabled"] = "true",
                ["Consigliere:Storage:RawTransactionPayloads:Provider"] = "fileSystem"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorePlatformZoneServices(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ConsigliereStorageConfig>>().Value);

        Assert.Contains("location.rootPath", exception.Failures.Single());
    }

    [Fact]
    public void AddCorePlatformZoneServices_RejectsAzosCacheBackend()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Network"] = "Mainnet",
                ["Consigliere:Cache:Enabled"] = "true",
                ["Consigliere:Cache:Backend"] = "azos",
                ["Consigliere:Cache:MaxEntries"] = "4096",
                ["Consigliere:Cache:Azos:TableName"] = "projection-cache"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorePlatformZoneServices(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ConsigliereCacheConfig>>().Value);

        Assert.Contains("Unsupported cache backend", exception.Failures.Single());
    }

    [Fact]
    public void AddCorePlatformZoneServices_UsesBuiltInProviderDefaultsWhenProvidersSectionIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Network"] = "Mainnet"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorePlatformZoneServices(configuration);

        using var provider = services.BuildServiceProvider();
        var sources = provider.GetRequiredService<IOptions<ConsigliereSourcesConfig>>().Value;

        Assert.Equal("hybrid", sources.Routing.PreferredMode);
        Assert.Equal("bitails", sources.Routing.PrimarySource);
        Assert.Equal(["junglebus", "node"], sources.Routing.FallbackSources);
        Assert.Equal("bitails", sources.Capabilities.RealtimeIngest.Source);
        Assert.Equal("bitails", sources.Capabilities.HistoricalAddressScan.Source);
        Assert.True(sources.Providers.Bitails.Enabled);
        Assert.Equal("https://api.bitails.io", sources.Providers.Bitails.Connection.BaseUrl);
        Assert.Equal("websocket", sources.Providers.Bitails.Connection.Transport);
    }

    [Fact]
    public void AddCorePlatformZoneServices_RejectsUnsupportedBitailsTransportOverrideOnlyInCodeDefaultsPath()
    {
        var sources = new ConsigliereSourcesConfig();
        sources.Providers.Bitails.Connection.Transport = "mqtt";

        var validator = new ConsigliereSourcesConfigValidation();
        var result = validator.Validate(string.Empty, sources);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, x => x.Contains("Unsupported Bitails transport"));
    }
}
