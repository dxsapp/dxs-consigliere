using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Transactions;

public sealed record TokenValidationDependencySnapshot(
    string TxId,
    IReadOnlyList<string> DependsOnTxIds,
    IReadOnlyList<string> MissingDependencies
)
{
    public static TokenValidationDependencySnapshot From(MetaTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var dependsOnTxIds = Normalize(
            (transaction.Inputs ?? [])
            .Select(x => x.TxId)
        );
        var missingDependencies = Normalize(
            (transaction.MissingTransactions ?? [])
            .Where(dependsOnTxIds.Contains)
        );

        return new TokenValidationDependencySnapshot(transaction.Id, dependsOnTxIds, missingDependencies);
    }

    public static TokenValidationDependencySnapshot Create(
        string txId,
        IEnumerable<string> dependsOnTxIds,
        IEnumerable<string> missingDependencies
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        var normalizedMissing = Normalize(missingDependencies);
        var normalizedDependsOn = Normalize((dependsOnTxIds ?? []).Concat(normalizedMissing));

        return new TokenValidationDependencySnapshot(
            txId,
            normalizedDependsOn,
            normalizedMissing.Where(normalizedDependsOn.Contains).ToArray()
        );
    }

    private static string[] Normalize(IEnumerable<string> txIds) =>
        (txIds ?? [])
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(x => x, StringComparer.Ordinal)
        .ToArray();
}
