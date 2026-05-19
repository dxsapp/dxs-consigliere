namespace Dxs.Consigliere.Services.Audit;

/// <summary>
/// wave-A3 S3 — fail-stop audit recorder. The slice contract
/// is that any destructive admin action runs <c>RecordAsync</c>
/// FIRST; if the write fails (Raven hiccup, expiry-bundle
/// disabled, etc.) the caller MUST abort. A "the broadcast
/// went out but there is no record of who clicked the button"
/// state is exactly what the audit trail exists to prevent.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Persist a single audit record. The <paramref name="context"/>
    /// argument is serialised with the default JSON options and
    /// stored as a string (the caller is responsible for keeping
    /// it free of raw transaction bytes / passwords / API keys).
    /// </summary>
    /// <returns><c>true</c> on a clean write; <c>false</c> if Raven refused.</returns>
    Task<bool> RecordAsync(
        string action,
        string targetId,
        object context,
        string username = null,
        CancellationToken cancellationToken = default);
}

public static class AuditActionNames
{
    /// <summary>Triggered every time <c>POST /api/tx/broadcast</c> reaches the network send.</summary>
    public const string BroadcastTx = "broadcast_tx";
}
