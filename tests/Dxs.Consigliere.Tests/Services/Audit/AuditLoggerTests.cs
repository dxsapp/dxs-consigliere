using Dxs.Consigliere.Services.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Raven.Client.Documents;

namespace Dxs.Consigliere.Tests.Services.Audit;

/// <summary>
/// wave-A3 S3-audit M1 — pin the fail-stop semantics around
/// the Raven expiration-bundle configuration call. Prior
/// revision swallowed the configure failure + still wrote the
/// audit document; the broadcast then proceeded without a
/// retention guarantee.
/// </summary>
public sealed class AuditLoggerTests
{
    [Fact]
    public async Task RecordAsync_returns_false_when_retention_configurator_throws()
    {
        var documentStore = new Mock<IDocumentStore>(MockBehavior.Strict);
        var configurator = new ThrowingConfigurator(throwAlways: true);

        var sut = new AuditLogger(
            documentStore.Object,
            configurator,
            new HttpContextAccessor(),
            NullLogger<AuditLogger>.Instance);

        var ok = await sut.RecordAsync(
            action: AuditActionNames.BroadcastTx,
            targetId: "tx-fixture",
            context: new { rawHexLength = 256, source = "test" });

        Assert.False(ok);
        // The session must NEVER have been opened — that's the
        // whole point: no write proceeds without a retention
        // guarantee. Strict-mode IDocumentStore would have
        // thrown on any unexpected call.
        documentStore.Verify(x => x.OpenAsyncSession(), Times.Never);
        Assert.Equal(1, configurator.Calls);
    }

    [Fact]
    public async Task Failed_configuration_re_arms_so_the_next_call_retries()
    {
        // Each subsequent RecordAsync call must reach the
        // configurator again. The production
        // RavenAuditRetentionConfigurator's
        // `Interlocked.CompareExchange` re-arm achieves this;
        // this stub mirrors the contract.
        var documentStore = new Mock<IDocumentStore>(MockBehavior.Strict);
        var configurator = new ThrowingConfigurator(throwAlways: true);

        var sut = new AuditLogger(
            documentStore.Object,
            configurator,
            new HttpContextAccessor(),
            NullLogger<AuditLogger>.Instance);

        await sut.RecordAsync("broadcast_tx", "tx-a", new { });
        await sut.RecordAsync("broadcast_tx", "tx-b", new { });

        Assert.Equal(2, configurator.Calls);
    }

    [Fact]
    public async Task RavenAuditRetentionConfigurator_rethrows_on_failure_and_re_arms_the_lazy_guard()
    {
        // Pin the production configurator's rethrow + re-arm
        // shape (the AuditLogger fail-stop relies on it). We
        // can't mock the sealed MaintenanceOperationExecutor,
        // so we drive a DocumentStore against an unreachable
        // host — SendAsync throws; the configurator re-arms;
        // a second call retries (and throws again).
        using var store = new DocumentStore
        {
            Urls = ["http://127.0.0.1:1"],
            Database = "consigliere-audit-test",
            Conventions = { DisableTopologyUpdates = true },
        };
        store.Initialize();

        var configurator = new RavenAuditRetentionConfigurator(store);

        await Assert.ThrowsAnyAsync<Exception>(() => configurator.EnsureConfiguredAsync(CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => configurator.EnsureConfiguredAsync(CancellationToken.None));
    }

    private sealed class ThrowingConfigurator(bool throwAlways) : IAuditRetentionConfigurator
    {
        public int Calls { get; private set; }

        public Task EnsureConfiguredAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return throwAlways
                ? Task.FromException(new InvalidOperationException("expiration bundle refused"))
                : Task.CompletedTask;
        }
    }
}
