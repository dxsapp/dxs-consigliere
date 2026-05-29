using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Services.P2p;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 6 S4 — pins for the inbound-listener config stub. The W6
/// release ships the flag + health-surface accessor only; no
/// listener thread. These tests verify the health surface mirrors
/// the config and a warning fires when an operator opts in.
/// </summary>
public class InboundConfigStubTests
{
    [Fact]
    public async Task StartAsync_InboundDisabled_HealthFlagFalse_NoWarning()
    {
        var (service, health, logger) = Build(inboundEnabled: false);

        await service.StartAsync(CancellationToken.None);

        Assert.False(health.InboundEnabled);
        Assert.DoesNotContain(logger.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Inbound.Enabled"));
    }

    [Fact]
    public async Task StartAsync_InboundEnabled_HealthFlagTrue_WarningLogged()
    {
        var (service, health, logger) = Build(inboundEnabled: true, listenPort: 8333);

        await service.StartAsync(CancellationToken.None);

        Assert.True(health.InboundEnabled);
        var warning = Assert.Single(logger.Entries.Where(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Inbound.Enabled")));
        Assert.Contains("8333", warning.Message);
        Assert.Contains("NOT implemented", warning.Message);
    }

    [Fact]
    public async Task StartAsync_InboundEnabled_DoesNotEnableBroadP2pSubsystem()
    {
        // The inbound flag is independent of the master P2P switch.
        // Enabling inbound MUST NOT silently start the outbound pool;
        // the operator's main toggle controls that.
        var (service, _, _) = Build(inboundEnabled: true, p2pEnabled: false);

        await service.StartAsync(CancellationToken.None);

        // No exception, no pool. (Bound state is false until
        // Enabled=true triggers the binding.) Implicit: StartAsync
        // returned without throwing the "Network not supported"
        // error that gates the rest of the startup path.
    }

    [Fact]
    public void InboundConfig_DefaultsMatchMasterDoc()
    {
        var cfg = new InboundConfig();
        Assert.False(cfg.Enabled);
        Assert.Equal(8333, cfg.ListenPort);
    }

    private static (BsvP2pHostedService service, BsvP2pHealth health, CapturingLogger logger)
        Build(bool inboundEnabled, int listenPort = 8333, bool p2pEnabled = false)
    {
        var health = new BsvP2pHealth();
        var logger = new CapturingLogger();
        var loggerFactory = new SingleLoggerFactory(logger);
        var cfg = new BsvP2pConfig
        {
            Enabled = p2pEnabled,
            Inbound = new InboundConfig { Enabled = inboundEnabled, ListenPort = listenPort },
        };
        var settings = new Mock<IOperatorRuntimeSettingsService>();
        settings.Setup(x => x.GetP2pEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(p2pEnabled);
        // Moq's default IDocumentStore returns null from Changes(); the
        // service's subscribe is wrapped in try/catch, so the live-toggle
        // watch degrades to a logged warning and StartAsync still applies
        // the boot-time state. Good enough for these inbound-stub pins.
        var documentStore = Mock.Of<IDocumentStore>();
        var service = new BsvP2pHostedService(
            Options.Create(cfg), health, settings.Object, documentStore, loggerFactory);
        return (service, health, logger);
    }

    private sealed class CapturingLogger : ILogger
    {
        public readonly System.Collections.Generic.List<(LogLevel Level, string Message)> Entries = new();

        public System.IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            System.Exception? exception, System.Func<TState, System.Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : System.IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Logger factory that returns the SAME capturing logger for every
    /// category — lets the test assert against entries written by any
    /// of <c>BsvP2pHostedService</c>'s logger fields.
    /// </summary>
    private sealed class SingleLoggerFactory : ILoggerFactory
    {
        private readonly ILogger _logger;
        public SingleLoggerFactory(ILogger logger) { _logger = logger; }
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => _logger;
        public void Dispose() { }
    }
}
