using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Setup;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Setup;

public class ConsigliereConfigBindingTests
{
    [Fact]
    public void AddCorePlatformZoneServices_BindsSourcesAndStorageOptions()
    {
        var values = new Dictionary<string, string?>
        {
            ["Network"] = "Mainnet",
            ["Consigliere:Sources:Routing:PreferredMode"] = "hybrid",
            ["Consigliere:Sources:Routing:PrimarySource"] = "junglebus",
            ["Consigliere:Sources:Routing:FallbackSources:0"] = "bitails",
            ["Consigliere:Sources:Routing:VerificationSource"] = "node",
            ["Consigliere:Sources:Providers:Node:Enabled"] = "true",
            ["Consigliere:Sources:Providers:Node:EnabledCapabilities:0"] = "broadcast",
            ["Consigliere:Sources:Providers:Node:Connection:RpcUrl"] = "http://127.0.0.1:8332",
            ["Consigliere:Sources:Capabilities:Broadcast:Mode"] = "multi",
            ["Consigliere:Sources:Capabilities:Broadcast:Sources:0"] = "node",
            ["Consigliere:Storage:RawTransactionPayloads:Enabled"] = "true",
            ["Consigliere:Storage:RawTransactionPayloads:Provider"] = "raven",
            ["Consigliere:Storage:RawTransactionPayloads:Location:Collection"] = "RawTransactionPayloads",
            ["Consigliere:Storage:RawTransactionPayloads:Compression:Enabled"] = "true",
            ["Consigliere:Storage:RawTransactionPayloads:Compression:Algorithm"] = "zstd"
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

        Assert.Equal("hybrid", sources.Routing.PreferredMode);
        Assert.Equal("junglebus", sources.Routing.PrimarySource);
        Assert.Equal("node", sources.Routing.VerificationSource);
        Assert.Equal(["bitails"], sources.Routing.FallbackSources);

        Assert.True(sources.Providers.Node.Enabled);
        Assert.Equal(["broadcast"], sources.Providers.Node.EnabledCapabilities);
        Assert.Equal("http://127.0.0.1:8332", sources.Providers.Node.Connection.RpcUrl);

        Assert.Equal("multi", sources.Capabilities.Broadcast.Mode);
        Assert.Equal(["node"], sources.Capabilities.Broadcast.Sources);

        Assert.True(storage.RawTransactionPayloads.Enabled);
        Assert.Equal("raven", storage.RawTransactionPayloads.Provider);
        Assert.Equal("RawTransactionPayloads", storage.RawTransactionPayloads.Location.Collection);
        Assert.NotNull(storage.RawTransactionPayloads.Compression);
        Assert.True(storage.RawTransactionPayloads.Compression.Enabled);
        Assert.Equal("zstd", storage.RawTransactionPayloads.Compression.Algorithm);
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
}
