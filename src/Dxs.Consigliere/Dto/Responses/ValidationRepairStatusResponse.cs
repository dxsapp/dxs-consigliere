namespace Dxs.Consigliere.Dto.Responses;

public sealed class ValidationRepairStatusResponse
{
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int RunningCount { get; set; }
    public int FailedCount { get; set; }
    public int BlockedCount { get; set; }
    public int ResolvedCount { get; set; }
    public long? OldestUnresolvedCreatedAt { get; set; }
    public int? OldestUnresolvedAgeSeconds { get; set; }
    public IReadOnlyList<ValidationRepairItemResponse> Items { get; set; } = [];
}

public sealed class ValidationRepairItemResponse
{
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string State { get; set; }
    public string[] Reasons { get; set; } = [];
    public string[] MissingDependencies { get; set; } = [];
    public int AttemptCount { get; set; }
    public long CreatedAt { get; set; }
    public long? UpdatedAt { get; set; }
    public long? LastAttemptAt { get; set; }
    public long? NextAttemptAt { get; set; }
    public string LastError { get; set; }
    public string[] LastFetchedDependencies { get; set; } = [];
}
