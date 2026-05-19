namespace Dxs.Consigliere.Dto.Responses.Admin;

/// <summary>
/// wave-A3 S3 — wire shape for the audit-log read surface.
/// Frozen contract: any field rename is a breaking change for
/// the admin UI's audit-log screen.
/// </summary>
public sealed class AdminAuditLogResponse
{
    /// <summary>Total entries that matched the filter set BEFORE pagination.</summary>
    public int TotalMatched { get; set; }

    /// <summary>Newest-first ordering.</summary>
    public AdminAuditLogEntryResponse[] Entries { get; set; } = [];
}

public sealed class AdminAuditLogEntryResponse
{
    public string Id { get; set; }
    public long UnixMs { get; set; }
    public string Username { get; set; }
    public string Action { get; set; }
    public string TargetId { get; set; }
    /// <summary>Pre-serialised JSON blob (string); the UI parses + renders.</summary>
    public string Context { get; set; }
}
