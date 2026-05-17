using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Observer;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Services.P2p;
using Dxs.Tests.Shared;

using Microsoft.Extensions.Logging.Abstractions;

using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 2 S3 — Raven-gated integration tests for the watchlist
/// loader. Non-Raven id-parsing coverage is in
/// <c>RavenWatchlistLoaderIdParsingTests</c>.
/// </summary>
public class RavenWatchlistLoaderTests : RavenTestDriver
{
    static RavenWatchlistLoaderTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false,
            },
        });
    }

    /// <summary>Real BSV mainnet address used in the existing
    /// Gate-3 fixtures; safe to use for hash160 derivation.</summary>
    private const string Addr1 = "12UScFvnuA9FjeoapbRQ5YS963mgzrQkZo";
    private const string Addr2 = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";

    [SkippableFact]
    public async Task InitializeAsync_BulkLoadsAllAddressesAndTokens()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");

        using var store = GetDocumentStore();

        // Seed two addresses + one token directly.
        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(new WatchingAddress { Address = Addr1, Name = "alice" }, $"address/{Addr1}");
            await session.StoreAsync(new WatchingAddress { Address = Addr2, Name = "bob" }, $"address/{Addr2}");
            await session.StoreAsync(new WatchingToken { TokenId = "tok-A", Symbol = "AAA" }, "token/tok-A/AAA");
            await session.SaveChangesAsync();
        }
        WaitForIndexing(store);

        var matcher = new WatchlistMatcher();
        await using var loader = new RavenWatchlistLoader(
            store, matcher, NullLogger<RavenWatchlistLoader>.Instance);

        await loader.InitializeAsync(CancellationToken.None);

        Assert.True(loader.IsLoaded);
        Assert.Equal(2, matcher.WatchedAddressCount);
        Assert.Equal(1, matcher.WatchedTokenCount);
        Assert.True(matcher.IsWatchedAddress(new Dxs.Bsv.Address(Addr1).Hash160));
        Assert.True(matcher.IsWatchedAddress(new Dxs.Bsv.Address(Addr2).Hash160));
        Assert.True(matcher.IsWatchedToken("tok-A"));
    }

    [SkippableFact]
    public async Task PutDelta_AddsToMatcher_AfterInitialise()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");

        using var store = GetDocumentStore();
        var matcher = new WatchlistMatcher();
        await using var loader = new RavenWatchlistLoader(
            store, matcher, NullLogger<RavenWatchlistLoader>.Instance);
        await loader.InitializeAsync(CancellationToken.None);

        // Add a new address after the loader is initialised.
        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(new WatchingAddress { Address = Addr1, Name = "alice" }, $"address/{Addr1}");
            await session.SaveChangesAsync();
        }

        // Changes API is asynchronous — poll up to 2s.
        var deadline = System.DateTime.UtcNow.AddSeconds(2);
        while (System.DateTime.UtcNow < deadline && matcher.WatchedAddressCount == 0)
            await Task.Delay(50);

        Assert.True(matcher.IsWatchedAddress(new Dxs.Bsv.Address(Addr1).Hash160),
            "matcher did not see Put delta within 2 s");
    }

    [SkippableFact]
    public async Task DeleteDelta_RemovesFromMatcher_AfterInitialise()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");

        using var store = GetDocumentStore();
        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(new WatchingAddress { Address = Addr1, Name = "alice" }, $"address/{Addr1}");
            await session.SaveChangesAsync();
        }

        var matcher = new WatchlistMatcher();
        await using var loader = new RavenWatchlistLoader(
            store, matcher, NullLogger<RavenWatchlistLoader>.Instance);
        await loader.InitializeAsync(CancellationToken.None);
        Assert.Equal(1, matcher.WatchedAddressCount);

        // Delete the address.
        using (var session = store.OpenAsyncSession())
        {
            session.Delete($"address/{Addr1}");
            await session.SaveChangesAsync();
        }

        // Poll up to 2s for the Delete event to propagate.
        var deadline = System.DateTime.UtcNow.AddSeconds(2);
        while (System.DateTime.UtcNow < deadline && matcher.WatchedAddressCount > 0)
            await Task.Delay(50);

        Assert.Equal(0, matcher.WatchedAddressCount);
        Assert.False(matcher.IsWatchedAddress(new Dxs.Bsv.Address(Addr1).Hash160));
    }

    [SkippableFact]
    public async Task TokenPutAndDelete_PropagateToMatcher()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");

        using var store = GetDocumentStore();
        var matcher = new WatchlistMatcher();
        await using var loader = new RavenWatchlistLoader(
            store, matcher, NullLogger<RavenWatchlistLoader>.Instance);
        await loader.InitializeAsync(CancellationToken.None);

        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(new WatchingToken { TokenId = "tok-Z", Symbol = "ZZZ" }, "token/tok-Z/ZZZ");
            await session.SaveChangesAsync();
        }

        var deadline = System.DateTime.UtcNow.AddSeconds(2);
        while (System.DateTime.UtcNow < deadline && !matcher.IsWatchedToken("tok-Z"))
            await Task.Delay(50);
        Assert.True(matcher.IsWatchedToken("tok-Z"));

        using (var session = store.OpenAsyncSession())
        {
            session.Delete("token/tok-Z/ZZZ");
            await session.SaveChangesAsync();
        }

        deadline = System.DateTime.UtcNow.AddSeconds(2);
        while (System.DateTime.UtcNow < deadline && matcher.IsWatchedToken("tok-Z"))
            await Task.Delay(50);
        Assert.False(matcher.IsWatchedToken("tok-Z"));
    }
}

/// <summary>
/// Non-Raven unit coverage for the static id-parsing helpers.
/// Deterministic on every environment.
/// </summary>
public class RavenWatchlistLoaderIdParsingTests
{
    [Theory]
    [InlineData("address/12UScFvnuA9FjeoapbRQ5YS963mgzrQkZo", true, "12UScFvnuA9FjeoapbRQ5YS963mgzrQkZo")]
    [InlineData("address/", false, "")]
    [InlineData("ADDRESS/x", false, "")]   // case-sensitive prefix
    [InlineData("transactions/abc", false, "")]
    [InlineData("", false, "")]
    public void TryExtractAddress_Coverage(string input, bool expectOk, string expectAddress)
    {
        var ok = RavenWatchlistLoader.TryExtractAddress(input, out var actual);
        Assert.Equal(expectOk, ok);
        if (expectOk) Assert.Equal(expectAddress, actual);
    }

    [Theory]
    [InlineData("token/tok-A/AAA", true, "tok-A")]
    [InlineData("token/tok-XYZ/ZZZ", true, "tok-XYZ")]
    [InlineData("token/tok-noSymbol", true, "tok-noSymbol")]
    [InlineData("token/", false, "")]
    [InlineData("TOKEN/x/y", false, "")]   // case-sensitive prefix
    [InlineData("", false, "")]
    public void TryExtractTokenId_Coverage(string input, bool expectOk, string expectTokenId)
    {
        var ok = RavenWatchlistLoader.TryExtractTokenId(input, out var actual);
        Assert.Equal(expectOk, ok);
        if (expectOk) Assert.Equal(expectTokenId, actual);
    }
}
