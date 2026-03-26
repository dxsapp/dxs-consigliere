using System.Buffers.Binary;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Transactions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Benchmarks.Shared;

internal static class VNextBenchmarkFixtureFactory
{
    public static string Address(int index) => CreateDeterministicAddress(index + 1);

    public static string IssuerAddress(int index) => CreateDeterministicAddress(1_000 + index);

    public static string TargetAddress(int index = 0) => CreateDeterministicAddress(index + 10_000);

    public static string TokenId(int index) => (index + 4096).ToString("x40");

    public static string IssueTxId(int index) => (index * 2).ToString("x64");

    public static string TransferTxId(int index) => (index * 2 + 1).ToString("x64");

    public static string BlockHash(int index) => (index + 8192).ToString("x64");

    public static string UnstableBlockHash(int index = 0) => (index + 12288).ToString("x64");

    private static string CreateDeterministicAddress(int seed)
    {
        Span<byte> hash160 = stackalloc byte[20];
        BinaryPrimitives.WriteInt32BigEndian(hash160[..4], seed);
        BinaryPrimitives.WriteInt32BigEndian(hash160[4..8], seed * 17);
        BinaryPrimitives.WriteInt32BigEndian(hash160[8..12], seed * 31);
        BinaryPrimitives.WriteInt32BigEndian(hash160[12..16], seed * 47);
        BinaryPrimitives.WriteInt32BigEndian(hash160[16..20], seed * 61);
        return new Address(hash160, ScriptType.P2PKH, Network.Mainnet).Value;
    }

    public static async Task SeedTransactionAsync(IDocumentStore store, MetaTransaction transaction, CancellationToken cancellationToken = default)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(transaction, transaction.Id, cancellationToken);

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
                ScriptPubKey = $"script-{transaction.Id}-{vout}",
                Spent = false
            }, output.Id, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public static async Task SeedTrackedAddressStatusAsync(
        IDocumentStore store,
        string address,
        string lifecycleStatus = TrackedEntityLifecycleStatus.Live,
        bool readable = true,
        bool authoritative = true,
        bool degraded = false,
        CancellationToken cancellationToken = default)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new TrackedAddressStatusDocument
        {
            Id = TrackedAddressStatusDocument.GetId(address),
            EntityType = TrackedEntityType.Address,
            EntityId = address,
            Address = address,
            Tracked = true,
            LifecycleStatus = lifecycleStatus,
            Readable = readable,
            Authoritative = authoritative,
            Degraded = degraded
        }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public static async Task SeedTrackedTokenStatusAsync(
        IDocumentStore store,
        string tokenId,
        string lifecycleStatus = TrackedEntityLifecycleStatus.Live,
        bool readable = true,
        bool authoritative = true,
        bool degraded = false,
        CancellationToken cancellationToken = default)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new TrackedTokenStatusDocument
        {
            Id = TrackedTokenStatusDocument.GetId(tokenId),
            EntityType = TrackedEntityType.Token,
            EntityId = tokenId,
            TokenId = tokenId,
            Tracked = true,
            LifecycleStatus = lifecycleStatus,
            Readable = readable,
            Authoritative = authoritative,
            Degraded = degraded
        }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public static async Task SeedTransactionScopeAsync(
        IDocumentStore store,
        string txId,
        IEnumerable<string> addresses,
        IEnumerable<string> tokenIds,
        CancellationToken cancellationToken = default)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new MetaTransaction
        {
            Id = txId,
            Addresses = addresses.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            TokenIds = tokenIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Inputs = [],
            Outputs = [],
            MissingTransactions = [],
            IllegalRoots = []
        }, txId, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public static MetaTransaction CreateTransaction(
        string txId,
        MetaTransaction.Input[]? inputs = null,
        MetaOutput[]? outputs = null,
        bool isIssue = false,
        bool isValidIssue = false,
        bool allStasInputsKnown = false,
        List<string>? illegalRoots = null,
        string? redeemAddress = null,
        DateTimeOffset? timestamp = null,
        int? height = null,
        string? block = null)
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
            Timestamp = (timestamp ?? DateTimeOffset.UnixEpoch).ToUnixTimeSeconds(),
            Height = height ?? MetaTransaction.DefaultHeight,
            Block = block
        };

    public static MetaTransaction.Input CreateInput(string txId, int vout)
        => new()
        {
            Id = MetaOutput.GetId(txId, vout),
            TxId = txId,
            Vout = vout
        };

    public static MetaOutput CreateOutput(string txId, int vout, string address, string? tokenId, long satoshis, ScriptType type)
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

    public static ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> CreateTxObservation(
        string eventType,
        string txId,
        long second,
        string? blockHash = null,
        int? blockHeight = null,
        string source = TxObservationSource.Node,
        int? transactionIndex = 0,
        string? removeReason = null)
    {
        var observation = new TxObservation(
            eventType,
            source,
            txId,
            DateTimeOffset.FromUnixTimeSeconds(1_710_100_000 + second),
            blockHash,
            blockHeight,
            transactionIndex,
            removeReason);

        var fingerprint = eventType == TxObservationEventType.SeenInBlock
            ? $"{source}|{eventType}|{txId}|{blockHash}"
            : $"{source}|{eventType}|{txId}";

        return new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
            new ObservationJournalEntry<TxObservation>(observation),
            new DedupeFingerprint(fingerprint));
    }

    public static ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>> CreateBlockObservation(
        string eventType,
        string blockHash,
        string source = TxObservationSource.Node)
    {
        var observation = new BlockObservation(eventType, source, blockHash);
        return new ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>>(
            new ObservationJournalEntry<BlockObservation>(observation),
            new DedupeFingerprint($"{source}|{eventType}|{blockHash}"));
    }

    public static Transaction CreateRuntimeTransaction(string txId, IEnumerable<string?> tokenIds, int rawByteLength = 8)
    {
        if (rawByteLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(rawByteLength));

        var raw = new byte[rawByteLength];
        for (var i = 0; i < raw.Length; i++)
            raw[i] = (byte)((i + txId.Length) % byte.MaxValue);

        var transaction = new Transaction(Network.Mainnet)
        {
            Id = txId,
            Raw = raw
        };

        var index = 0UL;
        foreach (var tokenId in tokenIds)
        {
            transaction.Outputs.Add(new Output
            {
                Address = new Address(Address((int)index + 1)),
                TokenId = tokenId,
                Type = string.IsNullOrWhiteSpace(tokenId) ? ScriptType.P2PKH : ScriptType.P2STAS,
                Satoshis = 1,
                Idx = index,
                ScriptPubKey = default
            });
            index++;
        }

        if (transaction.Outputs.Count == 0)
        {
            transaction.Outputs.Add(new Output
            {
                Address = new Address(Address(1)),
                TokenId = null,
                Type = ScriptType.P2PKH,
                Satoshis = 1,
                Idx = 0,
                ScriptPubKey = default
            });
        }

        return transaction;
    }

    public static FilteredTransactionMessage CreateFilteredMessage(string txId, IReadOnlyCollection<string> addresses, IReadOnlyCollection<string> tokenIds)
    {
        var transaction = CreateRuntimeTransaction(txId, tokenIds);
        return new FilteredTransactionMessage(transaction, addresses.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
