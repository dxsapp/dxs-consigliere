using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Models.Tracking;

namespace Dxs.Consigliere.Data.Tokens;

public sealed record TrackedTokenRootedHistoryEvaluation(
    string[] TrustedRoots,
    string[] CompletedTrustedRoots,
    string[] UnknownRoots,
    string[] CanonicalTxIds,
    string[] FrontierAddresses,
    bool HasMissingDependencies
)
{
    public int TrustedRootCount => TrustedRoots.Length;
    public int CompletedTrustedRootCount => CompletedTrustedRoots.Length;
    public int UnknownRootFindingCount => UnknownRoots.Length;
    public bool BlockingUnknownRoot => UnknownRoots.Length > 0;
    public bool RootedHistorySecure => UnknownRoots.Length == 0 && !HasMissingDependencies && CompletedTrustedRoots.Length == TrustedRoots.Length;
}

public static class TrackedTokenRootedHistoryEvaluator
{
    public static TrackedTokenRootedHistoryEvaluation Evaluate(
        string tokenId,
        IEnumerable<string>? trustedRoots,
        IEnumerable<MetaTransaction>? transactions
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        var normalizedRoots = Normalize(trustedRoots);
        var trustedRootSet = new HashSet<string>(normalizedRoots, StringComparer.OrdinalIgnoreCase);
        var txById = (transactions ?? [])
            .Where(x => x is not null)
            .Where(x => (x.TokenIds ?? []).Contains(tokenId, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        var unknownRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var memo = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasMissingDependencies = false;

        bool IsCanonical(MetaTransaction transaction)
        {
            if (!active.Add(transaction.Id))
                return false;

            try
            {
                if (memo.TryGetValue(transaction.Id, out var cached))
                    return cached;

                var result = EvaluateTransaction(transaction);
                memo[transaction.Id] = result;
                return result;
            }
            finally
            {
                active.Remove(transaction.Id);
            }
        }

        bool EvaluateTransaction(MetaTransaction transaction)
        {
            if (transaction.IsIssue)
            {
                if (!transaction.IsValidIssue)
                    return false;

                if (trustedRootSet.Contains(transaction.Id))
                    return true;

                unknownRoots.Add(transaction.Id);
                return false;
            }

            foreach (var illegalRoot in transaction.IllegalRoots ?? [])
            {
                if (!trustedRootSet.Contains(illegalRoot))
                    unknownRoots.Add(illegalRoot);
            }

            if ((transaction.IllegalRoots?.Count ?? 0) > 0)
                return false;

            if ((transaction.MissingTransactions?.Count ?? 0) > 0 || !transaction.AllStasInputsKnown)
            {
                hasMissingDependencies = true;
                return false;
            }

            var parentIds = (transaction.Inputs ?? [])
                .Select(x => x.TxId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(txById.ContainsKey)
                .ToArray();

            if (parentIds.Length == 0)
                return false;

            var tokenParents = parentIds
                .Select(id => txById[id])
                .Where(parent => (parent.TokenIds ?? []).Contains(tokenId, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            if (tokenParents.Length == 0)
                return false;

            return tokenParents.All(IsCanonical);
        }

        var canonicalTransactions = txById.Values
            .Where(IsCanonical)
            .ToArray();

        var completedRoots = normalizedRoots
            .Where(rootId => txById.TryGetValue(rootId, out var rootTransaction)
                && rootTransaction.IsIssue
                && rootTransaction.IsValidIssue)
            .ToArray();

        var frontierAddresses = canonicalTransactions
            .SelectMany(x => x.Outputs ?? [])
            .Where(x => string.Equals(x.TokenId, tokenId, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Address)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new TrackedTokenRootedHistoryEvaluation(
            normalizedRoots,
            completedRoots,
            unknownRoots.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            canonicalTransactions.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            frontierAddresses,
            hasMissingDependencies);
    }

    public static string[] Normalize(IEnumerable<string>? trustedRoots)
        => (trustedRoots ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
