#nullable enable
namespace Dxs.Consigliere.Services;

public interface IValidationDependencyService
{
    Task<ValidationDependencyResolutionResult> ResolveAsync(
        string entityId,
        IReadOnlyList<string> missingDependencies,
        CancellationToken cancellationToken = default);
}

public sealed record ValidationDependencyResolutionResult(
    IReadOnlyList<string> FetchedDependencies,
    IReadOnlyList<string> RemainingDependencies,
    string? LastError,
    string? StopReason,
    int FetchCount,
    int VisitedCount,
    int MaxTraversalDepth
);
