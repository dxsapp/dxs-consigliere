#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.P2p;

public sealed record PolicyValidationResult(
    bool IsValid,
    string? TxId,
    int SizeBytes,
    string? FailReason)
{
    public static PolicyValidationResult Ok(string txId, int size) =>
        new(true, txId, size, null);

    public static PolicyValidationResult Fail(string reason) =>
        new(false, null, 0, reason);
}

/// <summary>
/// Validates a raw transaction hex against local policy before any network
/// announce. Addresses audit finding C2 (Phase 1 safety gate).
/// </summary>
public sealed class TxPolicyValidator(
    IOptions<BsvP2pConfig> options,
    OutgoingTransactionStore store,
    INetworkProvider networkProvider)
{
    private readonly BsvP2pConfig _cfg = options.Value;

    public async Task<PolicyValidationResult> ValidateAsync(string rawHex, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawHex))
            return PolicyValidationResult.Fail("Empty transaction hex");

        // 1. Size cap (Phase 1: 2 MB)
        var sizeBytes = rawHex.Length / 2;
        var maxBytes = _cfg.TxPolicy?.MaxRawSizeBytes ?? BsvP2pConfig.DefaultTxMaxSizeBytes;
        if (sizeBytes > maxBytes)
            return PolicyValidationResult.Fail($"Transaction size {sizeBytes:N0} bytes exceeds limit {maxBytes:N0} bytes");

        // 2. Parse — get txid
        if (!Transaction.TryParse(rawHex, networkProvider.Network, out var parsed))
            return PolicyValidationResult.Fail("Unable to parse transaction hex");

        var txId = parsed.Id;

        // 3. Idempotency dedup
        var existing = await store.GetOrNullAsync(txId, ct);
        if (existing is not null)
            return PolicyValidationResult.Fail($"DUPLICATE:{txId}"); // caller checks for DUPLICATE: prefix

        // 4. Fee floor (optional — skip if not configured or fee is 0)
        // Fee calculation requires UTXO lookup which we don't have, so in Phase 1
        // we rely on the peer's feefilter to reject low-fee txs.
        // A configurable hard floor on parsed fee can be added in Phase 2.

        return PolicyValidationResult.Ok(txId, sizeBytes);
    }

    public bool IsDuplicateResult(PolicyValidationResult r) =>
        r.FailReason?.StartsWith("DUPLICATE:", StringComparison.Ordinal) == true;

    public string ExtractTxIdFromDuplicate(PolicyValidationResult r) =>
        r.FailReason!["DUPLICATE:".Length..];
}
