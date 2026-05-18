#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 5 A2 M1 fix — thin abstraction over the subset of
/// <see cref="TxPolicyValidator"/> operations the
/// <see cref="Services.IBroadcastService"/> uses. Lets the broadcast
/// service be unit-tested without standing up a real validator (which
/// internally couples to <c>IDocumentStore</c> for duplicate
/// detection).
/// </summary>
public interface IBroadcastPolicyValidator
{
    Task<PolicyValidationResult> ValidateAsync(string rawHex, CancellationToken ct = default);
    bool IsDuplicateResult(PolicyValidationResult result);
    string ExtractTxIdFromDuplicate(PolicyValidationResult result);
}
