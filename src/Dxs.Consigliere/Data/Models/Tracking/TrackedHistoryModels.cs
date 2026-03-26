namespace Dxs.Consigliere.Data.Models.Tracking;

public static class TrackedEntityHistoryMode
{
    public const string ForwardOnly = "forward_only";
    public const string FullHistory = "full_history";
}

public static class TrackedEntityHistoryReadiness
{
    public const string NotRequested = "not_requested";
    public const string ForwardLive = "forward_live";
    public const string BackfillingFullHistory = "backfilling_full_history";
    public const string FullHistoryLive = "full_history_live";
    public const string Degraded = "degraded";
}

public static class HistoryBackfillExecutionStatus
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string WaitingRetry = "waiting_retry";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public sealed class TrackedHistoryCoverage
{
    public string Mode { get; set; } = TrackedEntityHistoryMode.ForwardOnly;
    public bool FullCoverage { get; set; }
    public int? AuthoritativeFromBlockHeight { get; set; }
    public long? AuthoritativeFromObservedAt { get; set; }

    public TrackedHistoryCoverage Clone()
        => new()
        {
            Mode = Mode,
            FullCoverage = FullCoverage,
            AuthoritativeFromBlockHeight = AuthoritativeFromBlockHeight,
            AuthoritativeFromObservedAt = AuthoritativeFromObservedAt,
        };
}

public sealed class TrackedTokenHistorySecurityState
{
    public string[] TrustedRoots { get; set; } = [];
    public string[] UnknownRootFindings { get; set; } = [];
    public int CompletedTrustedRootCount { get; set; }
    public bool RootedHistorySecure { get; set; }
    public bool BlockingUnknownRoot { get; set; }

    public TrackedTokenHistorySecurityState Clone()
        => new()
        {
            TrustedRoots = TrustedRoots?.ToArray() ?? [],
            UnknownRootFindings = UnknownRootFindings?.ToArray() ?? [],
            CompletedTrustedRootCount = CompletedTrustedRootCount,
            RootedHistorySecure = RootedHistorySecure,
            BlockingUnknownRoot = BlockingUnknownRoot,
        };
}
