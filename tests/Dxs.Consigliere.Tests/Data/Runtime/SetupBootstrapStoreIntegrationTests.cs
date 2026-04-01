using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Runtime;

public class SetupBootstrapStoreIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task SaveAsync_PersistsSetupBootstrapDocumentWithDeterministicId()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var setupStore = new SetupBootstrapStore(store);

        await setupStore.SaveAsync(new SetupBootstrapDocument
        {
            SetupCompleted = true,
            AdminEnabled = true,
            AdminUsername = "operator",
            AdminPasswordHash = "hash",
            UpdatedBy = "operator"
        });

        using var session = store.OpenAsyncSession();
        var document = await session.LoadAsync<SetupBootstrapDocument>(SetupBootstrapDocument.DocumentId);

        Assert.NotNull(document);
        Assert.True(document.SetupCompleted);
        Assert.True(document.AdminEnabled);
        Assert.Equal("operator", document.AdminUsername);
    }
}
