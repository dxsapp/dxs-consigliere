using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Benchmarks.Shared;

public abstract class ConfiguredRavenBenchmarkTestDriver : RavenTestDriver
{
    static ConfiguredRavenBenchmarkTestDriver()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }
}
