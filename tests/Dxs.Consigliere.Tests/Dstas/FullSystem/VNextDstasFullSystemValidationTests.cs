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
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using Raven.Client.Documents;
using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.FullSystem;

public class VNextDstasFullSystemValidationTests : RavenTestDriver
{
    private const string IssuerAddress = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string HolderAddress = "1KFHE7w8BhaENAswwryaoccDb6qcT6DbYY";
    private const string ConfiscationReceiver = "1BoatSLRHtKNngkdXEeobR76b53LETtpyT";
    private const string TokenId = "1111111111111111111111111111111111111111";
    private const string IssueTxId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TransferTxId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string FreezeTxId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string UnfreezeTxId = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string ConfiscationTxId = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private const string SwapTxId = "f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0";
    private const string SwapCancelTxId = "abababababababababababababababababababababababababababababababab";
    private const string UnknownIssueTxId = "1212121212121212121212121212121212121212121212121212121212121212";
    private const string UnknownTransferTxId = "3434343434343434343434343434343434343434343434343434343434343434";
    private const string UnknownFreezeTxId = "5656565656565656565656565656565656565656565656565656565656565656";
    private const string UnknownHolderAddress = "1dice8EMZmqKvrGE4Qc9bUFf9PX3xaYDp";
    private const string StableBlockHash = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string StableTransferBlockHash = "2222222222222222222222222222222222222222222222222222222222222222";
    private const string UnstableBlockHash = "3333333333333333333333333333333333333333333333333333333333333333";

    static VNextDstasFullSystemValidationTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    [Fact]
    public async Task ReplayScenario_RebuildsDstasFreezeAndUnfreezeAcrossSurfaces()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        DstasNativeReplayProof.AssertConformanceVector("transfer_regular_valid");
        DstasNativeReplayProof.AssertConformanceVector("freeze_valid");
        DstasNativeReplayProof.AssertConformanceVector("unfreeze_valid");

        using var store = GetDocumentStore();
        await SeedTrackedScopeAsync(store, [IssuerAddress, HolderAddress], [TokenId]);
        await SeedTransactionAsync(store, CreateDstasIssue());
        await SeedTransactionAsync(store, CreateDstasTransfer());
        await SeedTransactionAsync(store, CreateDstasFreeze());
        await SeedTransactionAsync(store, CreateDstasUnfreeze());

        var txRebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var addressRebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var tokenRebuilder = new TokenProjectionRebuilder(store, new RavenObservationJournalReader(store), addressRebuilder);
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, IssueTxId, 1, StableBlockHash, 100));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, TransferTxId, 2, StableTransferBlockHash, 101));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInMempool, FreezeTxId, 3));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInMempool, UnfreezeTxId, 4));

        using var session = store.OpenAsyncSession();
        var freezeState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(FreezeTxId));
        var unfreezeState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(UnfreezeTxId));
        var holderTokenBalance = await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(HolderAddress, TokenId));
        var tokenState = await session.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(TokenId));
        var freezeHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, FreezeTxId));
        var unfreezeHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, UnfreezeTxId));
        var tokenReadiness = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(TokenId));
        var queryService = BuildQueryService(store);
        var freezeValidation = await queryService.ValidateStasTransactionAsync(FreezeTxId);
        var unfreezeValidation = await queryService.ValidateStasTransactionAsync(UnfreezeTxId);

        Assert.NotNull(freezeState);
        Assert.NotNull(unfreezeState);
        Assert.Equal(TxLifecycleStatus.SeenInMempool, freezeState!.LifecycleStatus);
        Assert.Equal(TxLifecycleStatus.SeenInMempool, unfreezeState!.LifecycleStatus);
        Assert.True(freezeState.Known);
        Assert.True(unfreezeState.Known);
        Assert.NotNull(holderTokenBalance);
        Assert.Equal(50, holderTokenBalance!.Satoshis);
        Assert.NotNull(tokenState);
        Assert.Equal(TokenProjectionProtocolType.Dstas, tokenState!.ProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Valid, tokenState.ValidationStatus);
        Assert.Equal(50, tokenState.TotalKnownSupply);
        Assert.NotNull(freezeHistory);
        Assert.NotNull(unfreezeHistory);
        Assert.Equal(TokenProjectionProtocolType.Dstas, freezeHistory!.ProtocolType);
        Assert.Equal(TokenProjectionProtocolType.Dstas, unfreezeHistory!.ProtocolType);
        Assert.NotNull(tokenReadiness);
        Assert.True(tokenReadiness!.Readable);
        Assert.True(tokenReadiness.Authoritative);
        Assert.False(freezeValidation.AskLater);
        Assert.Equal("freeze", freezeValidation.EventType);
        Assert.Equal(2, freezeValidation.SpendingType);
        Assert.Equal("unfreeze", unfreezeValidation.EventType);
        Assert.Equal(2, unfreezeValidation.SpendingType);
    }

    [Fact]
    public async Task ReorgScenario_RevertsDstasConfiscationAndRestoresOwnerState()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        DstasNativeReplayProof.AssertConformanceVector("confiscate_valid");
        DstasNativeReplayProof.AssertConformanceVector("confiscate_without_authority_rejected");
        DstasNativeReplayProof.AssertConformanceVector("confiscate_without_bit2_rejected");

        using var store = GetDocumentStore();
        await SeedTrackedScopeAsync(store, [IssuerAddress, HolderAddress, ConfiscationReceiver], [TokenId]);
        await SeedTransactionAsync(store, CreateDstasIssue());
        await SeedTransactionAsync(store, CreateDstasTransfer());
        await SeedTransactionAsync(store, CreateDstasConfiscation());

        var txRebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var addressRebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var tokenRebuilder = new TokenProjectionRebuilder(store, new RavenObservationJournalReader(store), addressRebuilder);
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var blockJournal = new RavenObservationJournal<BlockObservation>(store);
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, IssueTxId, 1, StableBlockHash, 100));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, TransferTxId, 2, StableTransferBlockHash, 101));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, ConfiscationTxId, 3, UnstableBlockHash, 102));
        await blockJournal.AppendAsync(CreateBlockObservation(BlockObservationEventType.Disconnected, UnstableBlockHash, 4));
        await txRebuilder.RebuildAsync();
        await addressRebuilder.RebuildAsync();
        await tokenRebuilder.RebuildAsync();

        using var session = store.OpenAsyncSession();
        var confiscationState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(ConfiscationTxId));
        var holderTokenBalance = await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(HolderAddress, TokenId));
        var confiscationBalance = await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(ConfiscationReceiver, TokenId));
        var tokenState = await session.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(TokenId));
        var confiscationHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, ConfiscationTxId));
        var queryService = BuildQueryService(store);
        var confiscationValidation = await queryService.ValidateStasTransactionAsync(ConfiscationTxId);

        Assert.NotNull(confiscationState);
        Assert.Equal(TxLifecycleStatus.Reorged, confiscationState!.LifecycleStatus);
        Assert.False(confiscationState.Authoritative);
        Assert.NotNull(holderTokenBalance);
        Assert.Equal(50, holderTokenBalance!.Satoshis);
        Assert.Null(confiscationBalance);
        Assert.NotNull(tokenState);
        Assert.Equal(TokenProjectionProtocolType.Dstas, tokenState!.ProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Valid, tokenState.ValidationStatus);
        Assert.Equal(50, tokenState.TotalKnownSupply);
        Assert.Null(confiscationHistory);
        Assert.Equal("confiscation", confiscationValidation.EventType);
        Assert.Equal(3, confiscationValidation.SpendingType);
    }

    [Fact]
    public async Task ReplayScenario_TracksDstasSwapAndSwapCancelQuerySemantics()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        DstasNativeReplayProof.AssertConformanceVector("swap_cancel_valid");

        using var store = GetDocumentStore();
        await SeedTrackedScopeAsync(store, [IssuerAddress, HolderAddress], [TokenId]);
        await SeedTransactionAsync(store, CreateDstasIssue());
        await SeedTransactionAsync(store, CreateDstasSwap());
        await SeedTransactionAsync(store, CreateDstasSwapCancel());

        var txRebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var addressRebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var tokenRebuilder = new TokenProjectionRebuilder(store, new RavenObservationJournalReader(store), addressRebuilder);
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, IssueTxId, 1, StableBlockHash, 100));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInMempool, SwapTxId, 2));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInMempool, SwapCancelTxId, 3));

        using var session = store.OpenAsyncSession();
        var swapState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(SwapTxId));
        var swapCancelState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(SwapCancelTxId));
        var holderTokenBalance = await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(HolderAddress, TokenId));
        var tokenState = await session.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(TokenId));
        var swapCancelHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, SwapCancelTxId));
        var tokenUtxos = await new TokenProjectionReader(store).LoadUtxosAsync(TokenId);
        var queryService = BuildQueryService(store);
        var swapValidation = await queryService.ValidateStasTransactionAsync(SwapTxId);
        var swapCancelValidation = await queryService.ValidateStasTransactionAsync(SwapCancelTxId);

        Assert.NotNull(swapState);
        Assert.NotNull(swapCancelState);
        Assert.Equal(TxLifecycleStatus.SeenInMempool, swapState!.LifecycleStatus);
        Assert.Equal(TxLifecycleStatus.SeenInMempool, swapCancelState!.LifecycleStatus);
        Assert.NotNull(holderTokenBalance);
        Assert.Equal(50, holderTokenBalance!.Satoshis);
        Assert.NotNull(tokenState);
        Assert.Equal(TokenProjectionProtocolType.Dstas, tokenState!.ProtocolType);
        Assert.Equal(50, tokenState.TotalKnownSupply);
        Assert.NotNull(swapCancelHistory);
        Assert.Equal(TokenProjectionProtocolType.Dstas, swapCancelHistory!.ProtocolType);
        Assert.Single(tokenUtxos);
        Assert.Equal(SwapCancelTxId, tokenUtxos[0].TxId);
        Assert.Equal("swap", swapValidation.EventType);
        Assert.Equal(1, swapValidation.SpendingType);
        Assert.Equal("swap_cancel", swapCancelValidation.EventType);
        Assert.Equal(4, swapCancelValidation.SpendingType);
    }

    private static TransactionQueryService BuildQueryService(IDocumentStore store)
        => new(
            store,
            new TxLifecycleProjectionReader(store),
            new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store)));

    private static MetaTransaction CreateDstasIssue()
        => CreateTransaction(
            IssueTxId,
            outputs: [CreateOutput(IssueTxId, 0, IssuerAddress, TokenId, 50, ScriptType.DSTAS, "empty", false)],
            isIssue: true,
            isValidIssue: true,
            redeemAddress: IssuerAddress,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_000));

    private static MetaTransaction CreateDstasTransfer()
        => CreateTransaction(
            TransferTxId,
            inputs: [CreateInput(IssueTxId, 0)],
            outputs: [CreateOutput(TransferTxId, 0, HolderAddress, TokenId, 50, ScriptType.DSTAS, "empty", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasSpendingType: 1,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_100));

    private static MetaTransaction CreateDstasFreeze()
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

    private static MetaTransaction CreateDstasUnfreeze()
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

    private static MetaTransaction CreateDstasConfiscation()
        => CreateTransaction(
            ConfiscationTxId,
            inputs: [CreateInput(TransferTxId, 0)],
            outputs: [CreateOutput(ConfiscationTxId, 0, ConfiscationReceiver, TokenId, 50, ScriptType.DSTAS, "confiscation", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "confiscation",
            dstasSpendingType: 3,
            dstasInputFrozen: true,
            dstasOutputFrozen: false,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_400));

    private static MetaTransaction CreateDstasSwap()
        => CreateTransaction(
            SwapTxId,
            inputs: [CreateInput(IssueTxId, 0)],
            outputs: [CreateOutput(SwapTxId, 0, HolderAddress, TokenId, 50, ScriptType.DSTAS, "swap", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "swap",
            dstasSpendingType: 1,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_500));

    private static MetaTransaction CreateDstasSwapCancel()
        => CreateTransaction(
            SwapCancelTxId,
            inputs: [CreateInput(SwapTxId, 0)],
            outputs: [CreateOutput(SwapCancelTxId, 0, HolderAddress, TokenId, 50, ScriptType.DSTAS, "empty", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "swap_cancel",
            dstasSpendingType: 4,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_600));

    private static MetaTransaction CreateUnknownRootDstasIssue()
        => CreateTransaction(
            UnknownIssueTxId,
            outputs: [CreateOutput(UnknownIssueTxId, 0, UnknownHolderAddress, TokenId, 70, ScriptType.DSTAS, "empty", false)],
            isIssue: true,
            isValidIssue: true,
            redeemAddress: IssuerAddress,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_700));

    private static MetaTransaction CreateUnknownRootDstasTransfer()
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

    private static MetaTransaction CreateUnknownRootDstasFreeze()
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

    private static async Task SeedTrackedScopeAsync(
        IDocumentStore store,
        string[] trackedAddresses,
        string[] trackedTokenIds,
        string[]? rootedTokenTrustedRoots = null)
    {
        using var session = store.OpenAsyncSession();
        foreach (var address in trackedAddresses)
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

        foreach (var tokenId in trackedTokenIds)
        {
            var isRooted = rootedTokenTrustedRoots is { Length: > 0 };
            await session.StoreAsync(new TrackedTokenDocument
            {
                Id = TrackedTokenDocument.GetId(tokenId),
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                TokenId = tokenId,
                Symbol = "DSTAS",
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                Degraded = false,
                HistoryMode = isRooted ? TrackedEntityHistoryMode.FullHistory : TrackedEntityHistoryMode.ForwardOnly,
                HistoryReadiness = isRooted ? TrackedEntityHistoryReadiness.FullHistoryLive : TrackedEntityHistoryReadiness.ForwardLive,
                HistoryCoverage = new TrackedHistoryCoverage
                {
                    Mode = isRooted ? TrackedEntityHistoryMode.FullHistory : TrackedEntityHistoryMode.ForwardOnly,
                    FullCoverage = isRooted,
                    AuthoritativeFromBlockHeight = 100,
                    AuthoritativeFromObservedAt = DateTimeOffset.Parse("2026-03-26T18:10:00Z").ToUnixTimeMilliseconds()
                },
                HistorySecurity = new TrackedTokenHistorySecurityState
                {
                    TrustedRoots = rootedTokenTrustedRoots ?? [],
                    CompletedTrustedRootCount = rootedTokenTrustedRoots?.Length ?? 0,
                    RootedHistorySecure = isRooted,
                    BlockingUnknownRoot = false
                }
            });
            await session.StoreAsync(new TrackedTokenStatusDocument
            {
                Id = TrackedTokenStatusDocument.GetId(tokenId),
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                TokenId = tokenId,
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                Degraded = false,
                HistoryMode = isRooted ? TrackedEntityHistoryMode.FullHistory : TrackedEntityHistoryMode.ForwardOnly,
                HistoryReadiness = isRooted ? TrackedEntityHistoryReadiness.FullHistoryLive : TrackedEntityHistoryReadiness.ForwardLive,
                HistoryCoverage = new TrackedHistoryCoverage
                {
                    Mode = isRooted ? TrackedEntityHistoryMode.FullHistory : TrackedEntityHistoryMode.ForwardOnly,
                    FullCoverage = isRooted,
                    AuthoritativeFromBlockHeight = 100,
                    AuthoritativeFromObservedAt = DateTimeOffset.Parse("2026-03-26T18:10:00Z").ToUnixTimeMilliseconds()
                },
                HistorySecurity = new TrackedTokenHistorySecurityState
                {
                    TrustedRoots = rootedTokenTrustedRoots ?? [],
                    CompletedTrustedRootCount = rootedTokenTrustedRoots?.Length ?? 0,
                    RootedHistorySecure = isRooted,
                    BlockingUnknownRoot = false
                }
            });
        }

        await session.SaveChangesAsync();
    }

    private static ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> CreateTxObservation(
        string eventType,
        string txId,
        long second,
        string? blockHash = null,
        int? blockHeight = null)
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

    private static ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>> CreateBlockObservation(
        string eventType,
        string blockHash,
        long second)
        => new(
            new ObservationJournalEntry<BlockObservation>(
                new BlockObservation(
                    eventType,
                    TxObservationSource.Node,
                    blockHash,
                    DateTimeOffset.FromUnixTimeSeconds(1_710_000_000 + second))),
            new DedupeFingerprint($"node|{eventType}|{blockHash}"));

    private static async Task AppendAndRebuildAsync(
        RavenObservationJournal<TxObservation> txJournal,
        TxLifecycleProjectionRebuilder txRebuilder,
        AddressProjectionRebuilder addressRebuilder,
        TokenProjectionRebuilder tokenRebuilder,
        ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> observation)
    {
        await txJournal.AppendAsync(observation);
        await txRebuilder.RebuildAsync();
        await addressRebuilder.RebuildAsync();
        await tokenRebuilder.RebuildAsync();
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
        string? dstasEventType = null,
        int? dstasSpendingType = null,
        bool? dstasInputFrozen = null,
        bool? dstasOutputFrozen = null,
        bool? dstasOptionalDataContinuity = null,
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
            DstasEventType = dstasEventType,
            DstasSpendingType = dstasSpendingType,
            DstasInputFrozen = dstasInputFrozen,
            DstasOutputFrozen = dstasOutputFrozen,
            DstasOptionalDataContinuity = dstasOptionalDataContinuity,
            Timestamp = (timestamp ?? DateTimeOffset.UnixEpoch).ToUnixTimeSeconds()
        };

    private static MetaTransaction.Input CreateInput(string txId, int vout)
        => new()
        {
            Id = MetaOutput.GetId(txId, vout),
            TxId = txId,
            Vout = vout
        };

    private static MetaOutput CreateOutput(
        string txId,
        int vout,
        string address,
        string? tokenId,
        long satoshis,
        ScriptType type,
        string? dstasActionType,
        bool? dstasFrozen)
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
