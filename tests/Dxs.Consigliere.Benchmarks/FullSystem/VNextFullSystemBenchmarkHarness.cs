using System.Diagnostics;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Data.Transactions;

using Raven.Client.Documents;
using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Benchmarks.FullSystem;

public sealed class VNextFullSystemBenchmarkHarness : RavenTestDriver
{
    static VNextFullSystemBenchmarkHarness()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    public async Task<VNextFullSystemBenchmarkMetrics> MeasureReplayAsync(
        VNextFullSystemBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        await SeedScenarioAsync(store, scenario, cancellationToken);

        var txRebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var addressRebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var tokenRebuilder = new TokenProjectionRebuilder(store, new RavenObservationJournalReader(store), addressRebuilder);

        var sw = Stopwatch.StartNew();
        await txRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        await addressRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        await tokenRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        sw.Stop();

        var operations = scenario.TransferCount * 2;
        return new VNextFullSystemBenchmarkMetrics(
            $"{scenario.Name}:replay",
            operations,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(operations, sw.ElapsedMilliseconds));
    }

    public async Task<VNextFullSystemBenchmarkMetrics> MeasureQueryBundleAsync(
        VNextFullSystemBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        await SeedScenarioAsync(store, scenario, cancellationToken);

        var journalReader = new RavenObservationJournalReader(store);
        var txRebuilder = new TxLifecycleProjectionRebuilder(store, journalReader);
        var addressRebuilder = new AddressProjectionRebuilder(store, journalReader);
        var tokenRebuilder = new TokenProjectionRebuilder(store, journalReader, addressRebuilder);

        await txRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        await addressRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        await tokenRebuilder.RebuildAsync(cancellationToken: cancellationToken);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < scenario.QueryCount; i++)
        {
            var txId = TransferTxId(i);
            var address = ReceiverAddress(i);
            var tokenId = TokenId(i);

            using var session = store.OpenAsyncSession();
            await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(txId), cancellationToken);
            await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(address, null), cancellationToken);
            await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(address, tokenId), cancellationToken);
            await session.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(tokenId), cancellationToken);
            await session.LoadAsync<TokenHistoryProjectionDocument>(
                [
                    TokenHistoryProjectionDocument.GetId(tokenId, IssueTxId(i)),
                    TokenHistoryProjectionDocument.GetId(tokenId, txId)
                ],
                cancellationToken);
            await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(IssuerAddress(i)), cancellationToken);
            await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken);
        }
        sw.Stop();

        var operations = scenario.QueryCount * 7;
        return new VNextFullSystemBenchmarkMetrics(
            $"{scenario.Name}:query",
            operations,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(operations, sw.ElapsedMilliseconds));
    }

    public async Task<VNextFullSystemBenchmarkMetrics> MeasureSoakAsync(
        VNextFullSystemBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        await SeedScenarioAsync(store, scenario, cancellationToken);

        var journalReader = new RavenObservationJournalReader(store);
        var txRebuilder = new TxLifecycleProjectionRebuilder(store, journalReader);
        var addressRebuilder = new AddressProjectionRebuilder(store, journalReader);
        var tokenRebuilder = new TokenProjectionRebuilder(store, journalReader, addressRebuilder);

        await txRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        await addressRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        await tokenRebuilder.RebuildAsync(cancellationToken: cancellationToken);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < scenario.SoakCycles; i++)
        {
            await txRebuilder.RebuildAsync(cancellationToken: cancellationToken);
            await addressRebuilder.RebuildAsync(cancellationToken: cancellationToken);
            await tokenRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        }
        sw.Stop();

        var operations = scenario.SoakCycles * 3;
        return new VNextFullSystemBenchmarkMetrics(
            $"{scenario.Name}:soak",
            operations,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(operations, sw.ElapsedMilliseconds));
    }

    private static async Task SeedScenarioAsync(IDocumentStore store, VNextFullSystemBenchmarkScenario scenario, CancellationToken cancellationToken)
    {
        var txJournal = new RavenObservationJournal<TxObservation>(store);

        for (var i = 0; i < scenario.TransferCount; i++)
        {
            await SeedTrackedScopeAsync(store, i, cancellationToken);
            await SeedTransactionAsync(
                store,
                CreateTransaction(
                    IssueTxId(i),
                    outputs:
                    [
                        CreateOutput(IssueTxId(i), 0, IssuerAddress(i), null, 1000, ScriptType.P2PKH),
                        CreateOutput(IssueTxId(i), 1, IssuerAddress(i), TokenId(i), 50, ScriptType.P2STAS)
                    ],
                    isIssue: true,
                    isValidIssue: true,
                    redeemAddress: IssuerAddress(i),
                    timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_100_000 + i)));

            await SeedTransactionAsync(
                store,
                CreateTransaction(
                    TransferTxId(i),
                    inputs:
                    [
                        CreateInput(IssueTxId(i), 0),
                        CreateInput(IssueTxId(i), 1)
                    ],
                    outputs:
                    [
                        CreateOutput(TransferTxId(i), 0, ReceiverAddress(i), null, 900, ScriptType.P2PKH),
                        CreateOutput(TransferTxId(i), 1, ReceiverAddress(i), TokenId(i), 50, ScriptType.P2STAS)
                    ],
                    allStasInputsKnown: true,
                    illegalRoots: [],
                    timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_100_500 + i)));

            await txJournal.AppendAsync(CreateTxObservation(TxObservationEventType.SeenInBlock, IssueTxId(i), i * 2L + 1, BlockHash(i), 100 + i), cancellationToken);
            await txJournal.AppendAsync(CreateTxObservation(i % 2 == 0 ? TxObservationEventType.SeenInBlock : TxObservationEventType.SeenInMempool, TransferTxId(i), i * 2L + 2, i % 2 == 0 ? BlockHash(i) : null, i % 2 == 0 ? 100 + i : null), cancellationToken);
        }
    }

    private static async Task SeedTrackedScopeAsync(IDocumentStore store, int index, CancellationToken cancellationToken)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new TrackedAddressDocument
        {
            Id = TrackedAddressDocument.GetId(IssuerAddress(index)),
            EntityType = TrackedEntityType.Address,
            EntityId = IssuerAddress(index),
            Address = IssuerAddress(index),
            Name = $"issuer-{index:x4}",
            Tracked = true,
            LifecycleStatus = TrackedEntityLifecycleStatus.Live,
            Readable = true,
            Authoritative = true,
            Degraded = false
        }, cancellationToken);
        await session.StoreAsync(new TrackedAddressStatusDocument
        {
            Id = TrackedAddressStatusDocument.GetId(IssuerAddress(index)),
            EntityType = TrackedEntityType.Address,
            EntityId = IssuerAddress(index),
            Address = IssuerAddress(index),
            Tracked = true,
            LifecycleStatus = TrackedEntityLifecycleStatus.Live,
            Readable = true,
            Authoritative = true,
            Degraded = false
        }, cancellationToken);
        await session.StoreAsync(new TrackedTokenDocument
        {
            Id = TrackedTokenDocument.GetId(TokenId(index)),
            EntityType = TrackedEntityType.Token,
            EntityId = TokenId(index),
            TokenId = TokenId(index),
            Symbol = $"TK{index:x2}",
            Tracked = true,
            LifecycleStatus = TrackedEntityLifecycleStatus.Live,
            Readable = true,
            Authoritative = true,
            Degraded = false
        }, cancellationToken);
        await session.StoreAsync(new TrackedTokenStatusDocument
        {
            Id = TrackedTokenStatusDocument.GetId(TokenId(index)),
            EntityType = TrackedEntityType.Token,
            EntityId = TokenId(index),
            TokenId = TokenId(index),
            Tracked = true,
            LifecycleStatus = TrackedEntityLifecycleStatus.Live,
            Readable = true,
            Authoritative = true,
            Degraded = false
        }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedTransactionAsync(IDocumentStore store, MetaTransaction transaction)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(transaction, transaction.Id);

        foreach (var output in transaction.Outputs)
        {
            await session.StoreAsync(new MetaOutput
            {
                Id = output.Id,
                TxId = transaction.Id,
                Vout = int.Parse(output.Id!.Split(':')[1]),
                Address = output.Address,
                TokenId = output.TokenId,
                Satoshis = output.Satoshis,
                Type = output.Type,
                ScriptPubKey = $"script-{transaction.Id}-{int.Parse(output.Id!.Split(':')[1])}",
                Spent = false
            }, output.Id);
        }

        await session.SaveChangesAsync();
    }

    private static ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> CreateTxObservation(
        string eventType,
        string txId,
        long second,
        string? blockHash,
        int? blockHeight)
    {
        var observation = new TxObservation(
            eventType,
            TxObservationSource.Node,
            txId,
            DateTimeOffset.FromUnixTimeSeconds(1_710_100_000 + second),
            blockHash,
            blockHeight,
            0);

        var fingerprint = eventType == TxObservationEventType.SeenInBlock
            ? $"node|{eventType}|{txId}|{blockHash}"
            : $"node|{eventType}|{txId}";

        return new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
            new ObservationJournalEntry<TxObservation>(observation),
            new DedupeFingerprint(fingerprint));
    }

    private static MetaTransaction CreateTransaction(
        string txId,
        MetaTransaction.Input[]? inputs = null,
        MetaOutput[]? outputs = null,
        bool isIssue = false,
        bool isValidIssue = false,
        bool allStasInputsKnown = false,
        List<string>? illegalRoots = null,
        string? redeemAddress = null,
        DateTimeOffset? timestamp = null)
        => new()
        {
            Id = txId,
            Inputs = inputs ?? [],
            Outputs = outputs?.Select(x => new MetaTransaction.Output(x)).ToList() ?? [],
            Addresses = outputs?.Select(x => x.Address).Where(x => x != null).Distinct().ToList() ?? [],
            TokenIds = outputs?.Select(x => x.TokenId).Where(x => x != null).Distinct().ToList() ?? [],
            IsStas = outputs?.Any(x => x.Type is ScriptType.P2STAS or ScriptType.DSTAS) == true,
            IsIssue = isIssue,
            IsValidIssue = isValidIssue,
            AllStasInputsKnown = allStasInputsKnown,
            IllegalRoots = illegalRoots ?? [],
            MissingTransactions = [],
            RedeemAddress = redeemAddress,
            Timestamp = (timestamp ?? DateTimeOffset.UnixEpoch).ToUnixTimeSeconds()
        };

    private static MetaTransaction.Input CreateInput(string txId, int vout)
        => new()
        {
            Id = MetaOutput.GetId(txId, vout),
            TxId = txId,
            Vout = vout
        };

    private static MetaOutput CreateOutput(string txId, int vout, string address, string? tokenId, long satoshis, ScriptType type)
        => new()
        {
            Id = MetaOutput.GetId(txId, vout),
            TxId = txId,
            Vout = vout,
            Address = address,
            TokenId = tokenId,
            Satoshis = satoshis,
            Type = type,
            ScriptPubKey = $"script-{txId}-{vout}",
            Spent = false
        };

    private static double ToThroughputPerSecond(int operations, long elapsedMilliseconds)
        => elapsedMilliseconds <= 0
            ? operations
            : operations * 1000.0 / elapsedMilliseconds;

    private static string IssueTxId(int index) => (index * 2).ToString("x64");
    private static string TransferTxId(int index) => (index * 2 + 1).ToString("x64");
    private static string TokenId(int index) => (index + 4096).ToString("x40");
    private static string BlockHash(int index) => (index + 8192).ToString("x64");
    private static string IssuerAddress(int index) => $"issuer-{index:x4}";
    private static string ReceiverAddress(int index) => $"receiver-{index:x4}";
}
