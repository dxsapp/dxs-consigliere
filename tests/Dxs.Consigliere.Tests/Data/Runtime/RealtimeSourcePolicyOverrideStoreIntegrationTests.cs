using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Runtime;

public class RealtimeSourcePolicyOverrideStoreIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task UpsertAsync_PersistsOverrideDocumentWithDeterministicId()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var overrideStore = new RealtimeSourcePolicyOverrideStore(store);

        await overrideStore.UpsertAsync("bitails", "zmq", "admin");

        using var session = store.OpenAsyncSession();
        var document = await session.LoadAsync<RealtimeSourcePolicyOverrideDocument>(RealtimeSourcePolicyOverrideDocument.DocumentId);

        Assert.NotNull(document);
        Assert.Equal("bitails", document.PrimaryRealtimeSource);
        Assert.Equal("zmq", document.BitailsTransport);
        Assert.Equal("admin", document.UpdatedBy);
        Assert.NotNull(document.UpdatedAt);
    }

    [Fact]
    public async Task ResetAsync_RemovesPersistedOverrideDocument()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var overrideStore = new RealtimeSourcePolicyOverrideStore(store);

        await overrideStore.UpsertAsync("bitails", "websocket", "admin");
        await overrideStore.ResetAsync();

        using var session = store.OpenAsyncSession();
        var document = await session.LoadAsync<RealtimeSourcePolicyOverrideDocument>(RealtimeSourcePolicyOverrideDocument.DocumentId);

        Assert.Null(document);
    }
}
