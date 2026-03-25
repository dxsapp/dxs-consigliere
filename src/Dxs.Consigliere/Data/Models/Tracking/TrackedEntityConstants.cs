namespace Dxs.Consigliere.Data.Models.Tracking;

public static class TrackedEntityType
{
    public const string Address = "address";
    public const string Token = "token";
}

public static class TrackedEntityLifecycleStatus
{
    public const string Registered = "registered";
    public const string Backfilling = "backfilling";
    public const string CatchingUp = "catching_up";
    public const string Live = "live";
    public const string Degraded = "degraded";
    public const string Paused = "paused";
    public const string Failed = "failed";
}
