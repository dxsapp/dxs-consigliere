using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Data.Transactions;
using Raven.Client.Documents;

namespace Dxs.Consigliere.Tests.Dstas.RootedHistory;

internal static class RootedDstasTestHarness
{
    public const string IssuerAddress = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    public const string HolderAddress = "1KFHE7w8BhaENAswwryaoccDb6qcT6DbYY";
    public const string TokenId = "1111111111111111111111111111111111111111";
    public const string IssueTxId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string TransferTxId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    public const string FreezeTxId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    public const string UnfreezeTxId = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    public const string UnknownIssueTxId = "1212121212121212121212121212121212121212121212121212121212121212";
    public const string UnknownTransferTxId = "3434343434343434343434343434343434343434343434343434343434343434";
    public const string UnknownFreezeTxId = "5656565656565656565656565656565656565656565656565656565656565656";
    public const string UnknownHolderAddress = "1dice8EMZmqKvrGE4Qc9bUFf9PX3xaYDp";
    public const string StableBlockHash = "1111111111111111111111111111111111111111111111111111111111111111";
    public const string StableTransferBlockHash = "2222222222222222222222222222222222222222222222222222222222222222";

    public static async Task SeedTrackedScopeAsync(IDocumentStore store, string[] rootedTokenTrustedRoots)
    {
        using var session = store.OpenAsyncSession();

        foreach (var address in new[] { IssuerAddress, HolderAddress, UnknownHolderAddress })
        {
            await session.StoreAsync(new TrackedAddressDocument
            {
                Id = TrackedAddressDocument.GetId(address),
                EntityType = TrackedEntityType.Address,
                EntityId = address,
                Address = address,
                Name = address,
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                Degraded = false
            });
            await session.StoreAsync(new TrackedAddressStatusDocument
            {
                Id = TrackedAddressStatusDocument.GetId(address),
                EntityType = TrackedEntityType.Address,
                EntityId = address,
                Address = address,
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                Degraded = false
            });
        }

        await session.StoreAsync(new TrackedTokenDocument
        {
            Id = TrackedTokenDocument.GetId(TokenId),
            EntityType = TrackedEntityType.Token,
            EntityId = TokenId,
            TokenId = TokenId,
            Symbol = "DSTAS",
            Tracked = true,
            LifecycleStatus = TrackedEntityLifecycleStatus.Live,
            Readable = true,
            Authoritative = true,
            Degraded = false,
            HistoryMode = TrackedEntityHistoryMode.FullHistory,
            HistoryReadiness = TrackedEntityHistoryReadiness.FullHistoryLive,
            HistoryCoverage = new TrackedHistoryCoverage
            {
                Mode = TrackedEntityHistoryMode.FullHistory,
                FullCoverage = true,
                AuthoritativeFromBlockHeight = 100,
                AuthoritativeFromObservedAt = DateTimeOffset.Parse("2026-03-26T18:10:00Z").ToUnixTimeMilliseconds()
            },
            HistorySecurity = new TrackedTokenHistorySecurityState
            {
                TrustedRoots = rootedTokenTrustedRoots,
                CompletedTrustedRootCount = rootedTokenTrustedRoots.Length,
                RootedHistorySecure = true,
                BlockingUnknownRoot = false
            }
        });
        await session.StoreAsync(new TrackedTokenStatusDocument
        {
            Id = TrackedTokenStatusDocument.GetId(TokenId),
            EntityType = TrackedEntityType.Token,
            EntityId = TokenId,
            TokenId = TokenId,
            Tracked = true,
            LifecycleStatus = TrackedEntityLifecycleStatus.Live,
            Readable = true,
            Authoritative = true,
            Degraded = false,
            HistoryMode = TrackedEntityHistoryMode.FullHistory,
            HistoryReadiness = TrackedEntityHistoryReadiness.FullHistoryLive,
            HistoryCoverage = new TrackedHistoryCoverage
            {
                Mode = TrackedEntityHistoryMode.FullHistory,
                FullCoverage = true,
                AuthoritativeFromBlockHeight = 100,
                AuthoritativeFromObservedAt = DateTimeOffset.Parse("2026-03-26T18:10:00Z").ToUnixTimeMilliseconds()
            },
            HistorySecurity = new TrackedTokenHistorySecurityState
            {
                TrustedRoots = rootedTokenTrustedRoots,
                CompletedTrustedRootCount = rootedTokenTrustedRoots.Length,
                RootedHistorySecure = true,
                BlockingUnknownRoot = false
            }
        });

        await session.SaveChangesAsync();
    }

    public static async Task SeedTrustedLifecycleAsync(IDocumentStore store)
    {
        await SeedTransactionAsync(store, CreateDstasIssue());
        await SeedTransactionAsync(store, CreateDstasTransfer());
        await SeedTransactionAsync(store, CreateDstasFreeze());
        await SeedTransactionAsync(store, CreateDstasUnfreeze());
    }

    public static async Task SeedUnknownRootLifecycleAsync(IDocumentStore store)
    {
        await SeedTransactionAsync(store, CreateUnknownRootDstasIssue());
        await SeedTransactionAsync(store, CreateUnknownRootDstasTransfer());
        await SeedTransactionAsync(store, CreateUnknownRootDstasFreeze());
    }

    public static (TxLifecycleProjectionRebuilder txRebuilder, AddressProjectionRebuilder addressRebuilder, TokenProjectionRebuilder tokenRebuilder, RavenObservationJournal<TxObservation> txJournal, TokenProjectionReader tokenReader) BuildProjectionStack(IDocumentStore store)
    {
        var journalReader = new RavenObservationJournalReader(store);
        var txRebuilder = new TxLifecycleProjectionRebuilder(store, journalReader);
        var addressRebuilder = new AddressProjectionRebuilder(store, journalReader);
        var tokenRebuilder = new TokenProjectionRebuilder(store, journalReader, addressRebuilder);
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var tokenReader = new TokenProjectionReader(store);
        return (txRebuilder, addressRebuilder, tokenRebuilder, txJournal, tokenReader);
    }

    public static async Task AppendTrustedLifecycleAsync(RavenObservationJournal<TxObservation> txJournal, TxLifecycleProjectionRebuilder txRebuilder, AddressProjectionRebuilder addressRebuilder, TokenProjectionRebuilder tokenRebuilder)
    {
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder, CreateTxObservation(TxObservationEventType.SeenInBlock, IssueTxId, 1, StableBlockHash, 100));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder, CreateTxObservation(TxObservationEventType.SeenInBlock, TransferTxId, 2, StableTransferBlockHash, 101));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder, CreateTxObservation(TxObservationEventType.SeenInMempool, FreezeTxId, 3));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder, CreateTxObservation(TxObservationEventType.SeenInMempool, UnfreezeTxId, 4));
    }

    public static async Task AppendUnknownRootLifecycleAsync(RavenObservationJournal<TxObservation> txJournal, TxLifecycleProjectionRebuilder txRebuilder, AddressProjectionRebuilder addressRebuilder, TokenProjectionRebuilder tokenRebuilder)
    {
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder, CreateTxObservation(TxObservationEventType.SeenInBlock, UnknownIssueTxId, 5, "4444444444444444444444444444444444444444444444444444444444444444", 103));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder, CreateTxObservation(TxObservationEventType.SeenInMempool, UnknownTransferTxId, 6));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder, CreateTxObservation(TxObservationEventType.SeenInMempool, UnknownFreezeTxId, 7));
    }

    public static ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> CreateTxObservation(string eventType, string txId, long second, string? blockHash = null, int? blockHeight = null)
    {
        var observation = new TxObservation(
            eventType,
            TxObservationSource.Node,
            txId,
            DateTimeOffset.FromUnixTimeSeconds(1_710_000_000 + second),
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

    public static async Task AppendAndRebuildAsync(RavenObservationJournal<TxObservation> txJournal, TxLifecycleProjectionRebuilder txRebuilder, AddressProjectionRebuilder addressRebuilder, TokenProjectionRebuilder tokenRebuilder, ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> observation)
    {
        await txJournal.AppendAsync(observation);
        await txRebuilder.RebuildAsync();
        await addressRebuilder.RebuildAsync();
        await tokenRebuilder.RebuildAsync();
    }

    public static MetaTransaction CreateDstasIssue()
        => CreateTransaction(
            IssueTxId,
            outputs: [CreateOutput(IssueTxId, 0, IssuerAddress, TokenId, 50, ScriptType.DSTAS, "empty", false)],
            isIssue: true,
            isValidIssue: true,
            redeemAddress: IssuerAddress,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_000));

    public static MetaTransaction CreateDstasTransfer()
        => CreateTransaction(
            TransferTxId,
            inputs: [CreateInput(IssueTxId, 0)],
            outputs: [CreateOutput(TransferTxId, 0, HolderAddress, TokenId, 50, ScriptType.DSTAS, "empty", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasSpendingType: 1,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_100));

    public static MetaTransaction CreateDstasFreeze()
        => CreateTransaction(
            FreezeTxId,
            inputs: [CreateInput(TransferTxId, 0)],
            outputs: [CreateOutput(FreezeTxId, 0, HolderAddress, TokenId, 50, ScriptType.DSTAS, "freeze", true)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "freeze",
            dstasSpendingType: 2,
            dstasInputFrozen: false,
            dstasOutputFrozen: true,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_200));

    public static MetaTransaction CreateDstasUnfreeze()
        => CreateTransaction(
            UnfreezeTxId,
            inputs: [CreateInput(FreezeTxId, 0)],
            outputs: [CreateOutput(UnfreezeTxId, 0, HolderAddress, TokenId, 50, ScriptType.DSTAS, "empty", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "unfreeze",
            dstasSpendingType: 2,
            dstasInputFrozen: true,
            dstasOutputFrozen: false,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_300));

    public static MetaTransaction CreateUnknownRootDstasIssue()
        => CreateTransaction(
            UnknownIssueTxId,
            outputs: [CreateOutput(UnknownIssueTxId, 0, UnknownHolderAddress, TokenId, 70, ScriptType.DSTAS, "empty", false)],
            isIssue: true,
            isValidIssue: true,
            redeemAddress: IssuerAddress,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_700));

    public static MetaTransaction CreateUnknownRootDstasTransfer()
        => CreateTransaction(
            UnknownTransferTxId,
            inputs: [CreateInput(UnknownIssueTxId, 0)],
            outputs: [CreateOutput(UnknownTransferTxId, 0, UnknownHolderAddress, TokenId, 70, ScriptType.DSTAS, "swap", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "swap",
            dstasSpendingType: 1,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_800));

    public static MetaTransaction CreateUnknownRootDstasFreeze()
        => CreateTransaction(
            UnknownFreezeTxId,
            inputs: [CreateInput(UnknownTransferTxId, 0)],
            outputs: [CreateOutput(UnknownFreezeTxId, 0, UnknownHolderAddress, TokenId, 70, ScriptType.DSTAS, "freeze", true)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "freeze",
            dstasSpendingType: 2,
            dstasInputFrozen: false,
            dstasOutputFrozen: true,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_900));

    private static MetaTransaction CreateTransaction(string txId, MetaTransaction.Input[]? inputs = null, MetaOutput[]? outputs = null, bool isIssue = false, bool isValidIssue = false, bool allStasInputsKnown = false, List<string>? illegalRoots = null, string? redeemAddress = null, string? dstasEventType = null, int? dstasSpendingType = null, bool? dstasInputFrozen = null, bool? dstasOutputFrozen = null, bool? dstasOptionalDataContinuity = null, DateTimeOffset? timestamp = null)
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
            DstasEventType = dstasEventType,
            DstasSpendingType = dstasSpendingType,
            DstasInputFrozen = dstasInputFrozen,
            DstasOutputFrozen = dstasOutputFrozen,
            DstasOptionalDataContinuity = dstasOptionalDataContinuity,
            StasProtocolType = TokenProjectionProtocolType.Dstas,
            StasValidationStatus = isIssue
                ? (isValidIssue ? TokenProjectionValidationStatus.Valid : TokenProjectionValidationStatus.Invalid)
                : ((illegalRoots?.Count ?? 0) > 0
                    ? TokenProjectionValidationStatus.Invalid
                    : allStasInputsKnown ? TokenProjectionValidationStatus.Valid : TokenProjectionValidationStatus.Unknown),
            CanProjectTokenOutputs = isIssue ? isValidIssue : allStasInputsKnown && (illegalRoots?.Count ?? 0) == 0,
            Timestamp = (timestamp ?? DateTimeOffset.UnixEpoch).ToUnixTimeSeconds()
        };

    private static MetaTransaction.Input CreateInput(string txId, int vout)
        => new() { Id = MetaOutput.GetId(txId, vout), TxId = txId, Vout = vout };

    private static MetaOutput CreateOutput(string txId, int vout, string address, string? tokenId, long satoshis, ScriptType type, string? dstasActionType, bool? dstasFrozen)
        => new()
        {
            Id = MetaOutput.GetId(txId, vout),
            TxId = txId,
            Vout = vout,
            Address = address,
            TokenId = tokenId,
            Satoshis = satoshis,
            Type = type,
            DstasActionType = dstasActionType,
            DstasFrozen = dstasFrozen,
            ScriptPubKey = $"script-{txId}-{vout}",
            Spent = false
        };

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
                DstasActionType = output.DstasActionType,
                DstasFrozen = output.DstasFrozen,
                ScriptPubKey = $"script-{transaction.Id}-{int.Parse(output.Id!.Split(':')[1])}",
                Spent = false
            }, output.Id);
        }

        await session.SaveChangesAsync();
    }
}
