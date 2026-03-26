using System.Diagnostics;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Tokens.Validation;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Benchmarks.Validation;

public sealed class TokenLineageBenchmarkHarness : RavenTestDriver
{
    static TokenLineageBenchmarkHarness()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    public Task<TokenLineageBenchmarkMetrics> MeasureEvaluationAsync(
        TokenLineageBenchmarkScenario scenario,
        CancellationToken cancellationToken = default
    )
    {
        var evaluator = new StasLineageEvaluator();
        var input = new StasLineageTransaction(
            "tx-benchmark",
            [
                new StasLineageInput(
                    "parent",
                    0,
                    2,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: "1ParentAddress",
                                TokenId: "token-benchmark",
                                Hash160: "issuer-benchmark",
                                DstasFrozen: false,
                                DstasActionType: "empty",
                                DstasOptionalDataFingerprint: "opt-benchmark"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.DSTAS,
                    Address: "1ReceiverAddress",
                    TokenId: "token-benchmark",
                    Hash160: "receiver-benchmark",
                    DstasFrozen: true,
                    DstasActionType: "freeze",
                    DstasOptionalDataFingerprint: "opt-benchmark"
                )
            ]
        );

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < scenario.EvaluationCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            evaluator.Evaluate(input);
        }
        sw.Stop();

        return Task.FromResult(new TokenLineageBenchmarkMetrics(
            $"{scenario.Name}:evaluate",
            scenario.EvaluationCount,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.EvaluationCount, sw.ElapsedMilliseconds)
        ));
    }

    public async Task<TokenLineageBenchmarkMetrics> MeasureRevalidationBurstAsync(
        TokenLineageBenchmarkScenario scenario,
        CancellationToken cancellationToken = default
    )
    {
        using var store = GetDocumentStore();
        var dependencyStore = new TokenValidationDependencyStore(store);
        var replayingStore = new RecordingMetaTransactionStore();
        var coordinator = new StasDependencyRevalidationCoordinator(store, replayingStore, NullLogger.Instance);
        replayingStore.AttachCoordinator(coordinator);

        await SeedMetaTransactionAsync(store, "root", cancellationToken);
        for (var i = 0; i < scenario.DependentCount; i++)
        {
            var txId = DependentTxId(i);
            await SeedMetaTransactionAsync(store, txId, cancellationToken);
            await dependencyStore.UpsertAsync(TokenValidationDependencySnapshot.Create(txId, ["root"], []), cancellationToken);
        }

        var sw = Stopwatch.StartNew();
        await coordinator.HandleTransactionChangedAsync("root", cancellationToken);
        sw.Stop();

        return new TokenLineageBenchmarkMetrics(
            $"{scenario.Name}:burst",
            scenario.DependentCount,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.DependentCount, sw.ElapsedMilliseconds)
        );
    }

    private static async Task SeedMetaTransactionAsync(
        Raven.Client.Documents.IDocumentStore store,
        string txId,
        CancellationToken cancellationToken
    )
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new MetaTransaction
        {
            Id = txId,
            Inputs = [],
            Outputs = []
        }, txId, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static string DependentTxId(int index) => $"dependent-{index:x8}";

    private static double ToThroughputPerSecond(int operations, long elapsedMilliseconds)
        => elapsedMilliseconds <= 0
            ? operations
            : operations * 1000.0 / elapsedMilliseconds;

    private sealed class RecordingMetaTransactionStore : IMetaTransactionStore
    {
        private StasDependencyRevalidationCoordinator? _coordinator;

        public void AttachCoordinator(StasDependencyRevalidationCoordinator coordinator)
            => _coordinator = coordinator;

        public async Task UpdateStasAttributes(string txId)
        {
            if (_coordinator is not null)
                await _coordinator.HandleTransactionChangedAsync(txId);
        }

        public Task<List<Address>> GetWatchingAddresses() => Task.FromResult(new List<Address>());

        public Task<List<TokenId>> GetWatchingTokens() => Task.FromResult(new List<TokenId>());

        public Task<TransactionProcessStatus> SaveTransaction(
            Transaction transaction,
            long timestamp,
            string firstOutToRedeem,
            string? blockHash = null,
            int? blockHeight = null,
            int? indexInBlock = null
        ) => Task.FromResult(TransactionProcessStatus.NotModified);

        public Task<Transaction?> TryRemoveTransaction(string id) => Task.FromResult<Transaction?>(null);
    }
}
