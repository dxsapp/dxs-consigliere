// Wave 5 S2: legacy multi-provider HTTP broadcast methods + helpers
// + Polly retry policy + multi-attempt Raven document writer all
// removed. The ctor dropped IBitcoindService / IBitailsRestApiClient
// / IWhatsOnChainRestApiClient dependencies — fee estimation moves
// to BitcoindService directly; client interfaces lose Broadcast in
// S3 + S4.
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Services.Impl;

public class BroadcastService(
    IBitcoindService bitcoindService,
    IDocumentStore documentStore,
    ILogger<BroadcastService> logger
) : IBroadcastService
{
    public Task<decimal> SatoshisPerByte() => bitcoindService.SatoshisPerByte();

    // Wave 5 S2: legacy Broadcast(string)/Broadcast(Transaction) +
    // multi-provider routing + Polly retry + BroadcastTo{Node,Bitails,
    // WhatsOnChain}Async + ResolveBroadcastTargetsAsync helpers were
    // deleted outright (no [Obsolete] shimming per vnext policy). The
    // overloads were also removed from IBroadcastService; the shape
    // test in tests/Broadcast/IBroadcastServiceShapeTests.cs asserts
    // their absence so a re-introduction fails the build.

    // ── Wave 2 Gate 3: P2P broadcast (renamed BroadcastAsync in W5 S0) ─

    // Property-injected via BroadcastServiceP2pExtension.Configure().
    // Kept as property injection to avoid coupling W5 to the BsvP2pSetup
    // wiring order; production DI sets them post-AddBsvP2pZoneServices.
    internal TxPolicyValidator PolicyValidator { get; set; }
    internal OutgoingTransactionStore OutgoingStore { get; set; }
    internal TxRelayCoordinator RelayCoordinator { get; set; }

    public async Task<BroadcastReceipt> BroadcastAsync(string rawHex, string clientConnectionId = null, CancellationToken ct = default)
    {
        if (PolicyValidator is null || OutgoingStore is null)
        {
            // P2P subsystem not enabled — degrade gracefully.
            return new BroadcastReceipt(null, OutgoingTxState.Failed, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                "P2P broadcast subsystem not enabled (Consigliere:Broadcast:P2p:Enabled = false)");
        }

        var validation = await PolicyValidator.ValidateAsync(rawHex, ct);

        if (!validation.IsValid)
        {
            if (PolicyValidator.IsDuplicateResult(validation))
            {
                // Idempotent: return existing receipt.
                var existing = await OutgoingStore.GetOrNullAsync(PolicyValidator.ExtractTxIdFromDuplicate(validation), ct);
                if (existing is not null)
                    return new BroadcastReceipt(existing.TxId, existing.State, existing.CreatedAtMs);
            }
            return new BroadcastReceipt(null, OutgoingTxState.PolicyInvalid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), validation.FailReason);
        }

        var tx = new OutgoingTransaction
        {
            Id = OutgoingTransaction.BuildId(validation.TxId),
            TxId = validation.TxId,
            RawHex = rawHex,
            ParsedSizeBytes = validation.SizeBytes,
            State = OutgoingTxState.Validated,
            ClientConnectionId = clientConnectionId,
        };
        await OutgoingStore.SaveAsync(tx, ct);

        // Fire-and-forget dispatch — lifecycle worker picks it up if this fails.
        _ = Task.Run(async () =>
        {
            try
            {
                tx.State = OutgoingTxState.Dispatching;
                await OutgoingStore.SaveAsync(tx, default);

                var served = await RelayCoordinator.AnnounceAsync(tx.TxId, tx.RawHex, default);
                if (served == 0)
                {
                    tx.State = OutgoingTxState.Failed;
                    tx.LastError = "No peers available at dispatch time";
                    await OutgoingStore.SaveAsync(tx, default);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background dispatch failed for {TxId}", tx.TxId);
            }
        }, ct);

        return new BroadcastReceipt(tx.TxId, OutgoingTxState.Validated, tx.CreatedAtMs);
    }
}
