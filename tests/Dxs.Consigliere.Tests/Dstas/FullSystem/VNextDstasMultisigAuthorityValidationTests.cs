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

public class VNextDstasMultisigAuthorityValidationTests : RavenTestDriver
{
    private const string IssuerAddress = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string MultisigOwnerAddress = "1P2MPKHOwner11111111111111111111111";
    private const string PostAuthorityOwnerAddress = "1Counterparty11111111111111111111111";
    private const string ConfiscationReceiver = "1BoatSLRHtKNngkdXEeobR76b53LETtpyT";
    private const string TokenId = "3333333333333333333333333333333333333333";
    private const string FreezeAuthority = "03-cat1|03-cat3|03-cat5";
    private const string ConfiscationAuthority = "03-cat1|03-cat2|03-cat4";
    private const string IssueTxId = "1010101010101010101010101010101010101010101010101010101010101010";
    private const string TransferToMultisigTxId = "2020202020202020202020202020202020202020202020202020202020202020";
    private const string FreezeTxId = "3030303030303030303030303030303030303030303030303030303030303030";
    private const string UnfreezeTxId = "4040404040404040404040404040404040404040404040404040404040404040";
    private const string ConfiscationTxId = "5050505050505050505050505050505050505050505050505050505050505050";
    private const string RejectedUnfreezeTxId = "6060606060606060606060606060606060606060606060606060606060606060";
    private const string StableBlockHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SecondBlockHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ThirdBlockHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    static VNextDstasMultisigAuthorityValidationTests()
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
    public async Task ReplayScenario_TracksMultisigAuthorityCycleAcrossSurfaces()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        DstasNativeReplayProof.AssertProtocolOwnerChain("authority_multisig_freeze_unfreeze_cycle");
        DstasNativeReplayProof.AssertConformanceVector("confiscate_valid");

        using var store = GetDocumentStore();
        await SeedTrackedScopeAsync(store, [IssuerAddress, MultisigOwnerAddress, ConfiscationReceiver], [TokenId]);
        await SeedTransactionAsync(store, CreateIssue());
        await SeedTransactionAsync(store, CreateTransferToMultisigOwner());
        await SeedTransactionAsync(store, CreateFreeze());
        await SeedTransactionAsync(store, CreateUnfreeze());
        await SeedTransactionAsync(store, CreateConfiscation());

        var txRebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var addressRebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var tokenRebuilder = new TokenProjectionRebuilder(store, new RavenObservationJournalReader(store), addressRebuilder);
        var txJournal = new RavenObservationJournal<TxObservation>(store);

        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, IssueTxId, 1, StableBlockHash, 100));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, TransferToMultisigTxId, 2, SecondBlockHash, 101));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInMempool, FreezeTxId, 3));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInMempool, UnfreezeTxId, 4));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, ConfiscationTxId, 5, ThirdBlockHash, 102));

        using var session = store.OpenAsyncSession();
        var freezeState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(FreezeTxId));
        var unfreezeState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(UnfreezeTxId));
        var confiscationState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(ConfiscationTxId));
        var multisigBalance = await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(MultisigOwnerAddress, TokenId));
        var confiscationBalance = await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(ConfiscationReceiver, TokenId));
        var freezeHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, FreezeTxId));
        var unfreezeHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, UnfreezeTxId));
        var confiscationHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, ConfiscationTxId));
        var tokenState = await session.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(TokenId));
        var tokenReadiness = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(TokenId));
        var queryService = BuildQueryService(store);
        var freezeValidation = await queryService.ValidateStasTransactionAsync(FreezeTxId);
        var unfreezeValidation = await queryService.ValidateStasTransactionAsync(UnfreezeTxId);
        var confiscationValidation = await queryService.ValidateStasTransactionAsync(ConfiscationTxId);

        Assert.NotNull(freezeState);
        Assert.NotNull(unfreezeState);
        Assert.NotNull(confiscationState);
        Assert.Equal(TxLifecycleStatus.SeenInMempool, freezeState!.LifecycleStatus);
        Assert.Equal(TxLifecycleStatus.SeenInMempool, unfreezeState!.LifecycleStatus);
        Assert.Equal(TxLifecycleStatus.Confirmed, confiscationState!.LifecycleStatus);
        Assert.Null(multisigBalance);
        Assert.NotNull(confiscationBalance);
        Assert.Equal(100, confiscationBalance!.Satoshis);
        Assert.NotNull(freezeHistory);
        Assert.NotNull(unfreezeHistory);
        Assert.NotNull(confiscationHistory);
        Assert.Equal(TokenProjectionProtocolType.Dstas, freezeHistory!.ProtocolType);
        Assert.Equal(TokenProjectionProtocolType.Dstas, unfreezeHistory!.ProtocolType);
        Assert.Equal(TokenProjectionProtocolType.Dstas, confiscationHistory!.ProtocolType);
        Assert.NotNull(tokenState);
        Assert.Equal(TokenProjectionProtocolType.Dstas, tokenState!.ProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Valid, tokenState.ValidationStatus);
        Assert.Equal(100, tokenState.TotalKnownSupply);
        Assert.NotNull(tokenReadiness);
        Assert.True(tokenReadiness!.Readable);
        Assert.True(tokenReadiness.Authoritative);
        Assert.Equal("freeze", freezeValidation.EventType);
        Assert.Equal(2, freezeValidation.SpendingType);
        Assert.Equal("unfreeze", unfreezeValidation.EventType);
        Assert.Equal(2, unfreezeValidation.SpendingType);
        Assert.Equal("confiscation", confiscationValidation.EventType);
        Assert.Equal(3, confiscationValidation.SpendingType);
    }

    [Fact]
    public async Task ReplayScenario_KnownButUnobservedAuthorityAttemptDoesNotAdvanceWorldState()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        DstasNativeReplayProof.AssertProtocolOwnerChain("owner_multisig_positive_spend");

        using var store = GetDocumentStore();
        await SeedTrackedScopeAsync(store, [IssuerAddress, MultisigOwnerAddress], [TokenId]);
        await SeedTransactionAsync(store, CreateIssue());
        await SeedTransactionAsync(store, CreateTransferToMultisigOwner());
        await SeedTransactionAsync(store, CreateFreeze());
        await SeedTransactionAsync(store, CreateRejectedUnfreezeAttempt());

        var txRebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var addressRebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var tokenRebuilder = new TokenProjectionRebuilder(store, new RavenObservationJournalReader(store), addressRebuilder);
        var txJournal = new RavenObservationJournal<TxObservation>(store);

        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, IssueTxId, 1, StableBlockHash, 100));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInBlock, TransferToMultisigTxId, 2, SecondBlockHash, 101));
        await AppendAndRebuildAsync(txJournal, txRebuilder, addressRebuilder, tokenRebuilder,
            CreateTxObservation(TxObservationEventType.SeenInMempool, FreezeTxId, 3));

        using var session = store.OpenAsyncSession();
        var freezeState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(FreezeTxId));
        var rejectedState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(RejectedUnfreezeTxId));
        var multisigBalance = await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(MultisigOwnerAddress, TokenId));
        var rejectedHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, RejectedUnfreezeTxId));
        var queryService = BuildQueryService(store);
        var rejectedValidation = await queryService.ValidateStasTransactionAsync(RejectedUnfreezeTxId);

        Assert.NotNull(freezeState);
        Assert.Equal(TxLifecycleStatus.SeenInMempool, freezeState!.LifecycleStatus);
        Assert.Null(rejectedState);
        Assert.NotNull(multisigBalance);
        Assert.Equal(100, multisigBalance!.Satoshis);
        Assert.Null(rejectedHistory);
        Assert.Equal("unfreeze", rejectedValidation.EventType);
        Assert.Equal(2, rejectedValidation.SpendingType);
    }

    private static TransactionQueryService BuildQueryService(IDocumentStore store)
        => new(
            store,
            new TxLifecycleProjectionReader(store),
            new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store)));

    private static MetaTransaction CreateIssue()
        => CreateTransaction(
            IssueTxId,
            outputs: [CreateOutput(IssueTxId, 0, IssuerAddress, TokenId, 100, ScriptType.DSTAS, "empty", false)],
            isIssue: true,
            isValidIssue: true,
            redeemAddress: IssuerAddress,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_010_000));

    private static MetaTransaction CreateTransferToMultisigOwner()
        => CreateTransaction(
            TransferToMultisigTxId,
            inputs: [CreateInput(IssueTxId, 0)],
            outputs: [CreateOutput(TransferToMultisigTxId, 0, MultisigOwnerAddress, TokenId, 100, ScriptType.DSTAS, "empty", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasSpendingType: 1,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_010_100));

    private static MetaTransaction CreateFreeze()
        => CreateTransaction(
            FreezeTxId,
            inputs: [CreateInput(TransferToMultisigTxId, 0)],
            outputs: [CreateOutput(FreezeTxId, 0, MultisigOwnerAddress, TokenId, 100, ScriptType.DSTAS, "freeze", true)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "freeze",
            dstasSpendingType: 2,
            dstasInputFrozen: false,
            dstasOutputFrozen: true,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_010_200));

    private static MetaTransaction CreateUnfreeze()
        => CreateTransaction(
            UnfreezeTxId,
            inputs: [CreateInput(FreezeTxId, 0)],
            outputs: [CreateOutput(UnfreezeTxId, 0, MultisigOwnerAddress, TokenId, 100, ScriptType.DSTAS, "empty", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "unfreeze",
            dstasSpendingType: 2,
            dstasInputFrozen: true,
            dstasOutputFrozen: false,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_010_300));

    private static MetaTransaction CreateConfiscation()
        => CreateTransaction(
            ConfiscationTxId,
            inputs: [CreateInput(UnfreezeTxId, 0)],
            outputs: [CreateOutput(ConfiscationTxId, 0, ConfiscationReceiver, TokenId, 100, ScriptType.DSTAS, "confiscation", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "confiscation",
            dstasSpendingType: 3,
            dstasInputFrozen: false,
            dstasOutputFrozen: false,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_010_400));

    private static MetaTransaction CreateRejectedUnfreezeAttempt()
        => CreateTransaction(
            RejectedUnfreezeTxId,
            inputs: [CreateInput(FreezeTxId, 0)],
            outputs: [CreateOutput(RejectedUnfreezeTxId, 0, PostAuthorityOwnerAddress, TokenId, 100, ScriptType.DSTAS, "empty", false)],
            allStasInputsKnown: true,
            illegalRoots: [],
            dstasEventType: "unfreeze",
            dstasSpendingType: 2,
            dstasInputFrozen: true,
            dstasOutputFrozen: false,
            dstasOptionalDataContinuity: true,
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_010_350));

    private static async Task SeedTrackedScopeAsync(IDocumentStore store, string[] trackedAddresses, string[] trackedTokenIds)
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
            await session.StoreAsync(new TrackedTokenDocument
            {
                Id = TrackedTokenDocument.GetId(tokenId),
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                TokenId = tokenId,
                Symbol = "DSTAS-MSIG",
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                Degraded = false
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
                Degraded = false
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
            DateTimeOffset.FromUnixTimeSeconds(1_710_010_000 + second),
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
            DstasFreezeEnabled = true,
            DstasConfiscationEnabled = true,
            DstasFreezeAuthority = FreezeAuthority,
            DstasConfiscationAuthority = ConfiscationAuthority,
            DstasServiceFields = [FreezeAuthority, ConfiscationAuthority],
            ScriptPubKey = $"script-{txId}-{vout}",
            Spent = false
        };

    private static async Task SeedTransactionAsync(IDocumentStore store, MetaTransaction transaction)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(transaction, transaction.Id);

        foreach (var output in transaction.Outputs)
        {
            var vout = int.Parse(output.Id!.Split(':')[1]);
            await session.StoreAsync(new MetaOutput
            {
                Id = output.Id,
                TxId = transaction.Id,
                Vout = vout,
                Address = output.Address,
                TokenId = output.TokenId,
                Satoshis = output.Satoshis,
                Type = output.Type,
                DstasActionType = output.DstasActionType,
                DstasFrozen = output.DstasFrozen,
                DstasFreezeEnabled = output.DstasFreezeEnabled,
                DstasConfiscationEnabled = output.DstasConfiscationEnabled,
                DstasFreezeAuthority = output.DstasFreezeAuthority,
                DstasConfiscationAuthority = output.DstasConfiscationAuthority,
                DstasServiceFields = output.DstasServiceFields,
                ScriptPubKey = $"script-{transaction.Id}-{vout}",
                Spent = false
            }, output.Id);
        }

        await session.SaveChangesAsync();
    }
}
