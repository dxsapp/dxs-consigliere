using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Tests.Dstas.Persistence;

public class DstasProtocolTruthOracleTests
{
    [Fact]
    public void SharedLoaders_ReadFromValidatedOracleFixtures()
    {
        var vectors = DstasConformanceVectorFixture.LoadAll();
        var chains = DstasProtocolOwnerFixture.LoadAll();

        Assert.NotEmpty(vectors);
        Assert.NotEmpty(chains);
        Assert.All(vectors, x => Assert.False(string.IsNullOrWhiteSpace(x.TxHex)));
        Assert.All(chains, x => Assert.NotEmpty(x.Transactions));
    }
}
