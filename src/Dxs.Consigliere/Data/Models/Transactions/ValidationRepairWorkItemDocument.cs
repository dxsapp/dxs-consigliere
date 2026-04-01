#nullable enable
using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Data.Models.Transactions;

public sealed class ValidationRepairWorkItemDocument : AuditableEntity
{
    public string EntityType { get; set; } = ValidationRepairEntityTypes.Transaction;
    public string EntityId { get; set; } = string.Empty;
    public string[] Reasons { get; set; } = [];
    public string[] MissingDependencies { get; set; } = [];
    public string State { get; set; } = ValidationRepairStates.Pending;
    public int AttemptCount { get; set; }
    public long? LastAttemptAt { get; set; }
    public long? NextAttemptAt { get; set; }
    public long? LastResolvedAt { get; set; }
    public string LastError { get; set; } = string.Empty;
    public string[] LastFetchedDependencies { get; set; } = [];
    public string? LastStopReason { get; set; }
    public int LastFetchCount { get; set; }
    public int LastVisitedCount { get; set; }
    public int LastTraversalDepth { get; set; }

    public override string GetId() => GetId(EntityId);

    public static string GetId(string entityId) => $"tx/validation/repair/{entityId}";

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        yield return nameof(EntityType);
        yield return nameof(EntityId);
        yield return nameof(Reasons);
        yield return nameof(MissingDependencies);
        yield return nameof(State);
        yield return nameof(AttemptCount);
        yield return nameof(LastAttemptAt);
        yield return nameof(NextAttemptAt);
        yield return nameof(LastResolvedAt);
        yield return nameof(LastError);
        yield return nameof(LastFetchedDependencies);
        yield return nameof(LastStopReason);
        yield return nameof(LastFetchCount);
        yield return nameof(LastVisitedCount);
        yield return nameof(LastTraversalDepth);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        yield return nameof(EntityType);
        yield return nameof(EntityId);
        yield return nameof(Reasons);
        yield return nameof(MissingDependencies);
        yield return nameof(State);
        yield return nameof(AttemptCount);
        yield return nameof(LastAttemptAt);
        yield return nameof(NextAttemptAt);
        yield return nameof(LastResolvedAt);
        yield return nameof(LastError);
        yield return nameof(LastFetchedDependencies);
        yield return nameof(LastStopReason);
        yield return nameof(LastFetchCount);
        yield return nameof(LastVisitedCount);
        yield return nameof(LastTraversalDepth);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        yield return new(nameof(EntityType), EntityType);
        yield return new(nameof(EntityId), EntityId);
        yield return new(nameof(Reasons), Reasons);
        yield return new(nameof(MissingDependencies), MissingDependencies);
        yield return new(nameof(State), State);
        yield return new(nameof(AttemptCount), AttemptCount);
        yield return new(nameof(LastAttemptAt), LastAttemptAt);
        yield return new(nameof(NextAttemptAt), NextAttemptAt);
        yield return new(nameof(LastResolvedAt), LastResolvedAt);
        yield return new(nameof(LastError), LastError);
        yield return new(nameof(LastFetchedDependencies), LastFetchedDependencies);
        yield return new(nameof(LastStopReason), LastStopReason);
        yield return new(nameof(LastFetchCount), LastFetchCount);
        yield return new(nameof(LastVisitedCount), LastVisitedCount);
        yield return new(nameof(LastTraversalDepth), LastTraversalDepth);
    }
}

public static class ValidationRepairEntityTypes
{
    public const string Transaction = "transaction";
}

public static class ValidationRepairStates
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Resolved = "resolved";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
}

public static class ValidationRepairReasons
{
    public const string PublicValidate = "public_validate";
    public const string PeriodicUnresolvedScan = "periodic_unresolved_scan";
    public const string RootedHistorySync = "rooted_history_sync";
    public const string MissingParentRepair = "missing_parent_repair";
    public const string LateArrivalRepair = "late_arrival_repair";
}

public static class ValidationRepairStopReasons
{
    public const string TrustedRootReached = "trusted_root_reached";
    public const string ValidIssueReached = "valid_issue_reached";
    public const string IllegalRootFound = "illegal_root_found";
    public const string AlreadyVisited = "already_visited";
    public const string MissingDependency = "missing_dependency";
    public const string BudgetExceeded = "budget_exceeded";
    public const string ProviderRateLimited = "provider_rate_limited";
    public const string ProviderError = "provider_error";
}
