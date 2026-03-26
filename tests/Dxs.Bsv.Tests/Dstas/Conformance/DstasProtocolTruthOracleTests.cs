using Dxs.Tests.Shared;

namespace Dxs.Bsv.Tests.Dstas.Conformance;

public class DstasProtocolTruthOracleTests
{
    [Fact]
    public void ValidateAll_VendoredFixturesMatchManifestAndMirrors()
    {
        var results = DstasProtocolTruthOracle.ValidateAll();

        Assert.True(results.Count >= 3);
        Assert.Contains(results, x => x.FixtureId == "dstas_conformance_vectors");
        Assert.Contains(results, x => x.FixtureId == "dstas_protocol_owner_fixtures");
    }
}
