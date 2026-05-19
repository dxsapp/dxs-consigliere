namespace Dxs.Consigliere.Data.Models.Audit;

/// <summary>
/// wave-A3 S3 — immutable forensic record for a destructive
/// admin action. Stored in Raven under the
/// <c>AuditLogEntries</c> collection. A 365-day @expires
/// metadata header is attached at write time so the audit
/// trail doesn't grow without bound; operators can extend the
/// retention by editing the metadata before the TTL fires.
///
/// Once written, audit entries are read-only across every
/// admin surface — there is no UI affordance to mutate or
/// delete them.
/// </summary>
public sealed class AuditLogEntry : Entity
{
    /// <summary>
    /// Stable id prefix; the actual document id is
    /// <c>audit-log/&lt;unixMs&gt;/&lt;guid8&gt;</c>. The
    /// timestamp prefix gives Raven a natural ordering for
    /// range queries.
    /// </summary>
    public const string IdPrefix = "audit-log/";

    /// <summary>Wall-clock timestamp at the moment of recording.</summary>
    public long UnixMs { get; set; }

    /// <summary>Admin user identity that triggered the action. "anonymous" if missing.</summary>
    public string Username { get; set; }

    /// <summary>Stable action name (e.g. <c>broadcast_tx</c>, <c>config_update</c>).</summary>
    public string Action { get; set; }

    /// <summary>Free-form target identifier (txid, address, providerId, etc.).</summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Opaque JSON blob with extra context for the action.
    /// Stored as a serialised string so the schema is the
    /// caller's responsibility. MUST NOT contain raw transaction
    /// bytes / passwords / API keys (the call site sanitises).
    /// </summary>
    public string Context { get; set; }

    public override string GetId() => Id;

    public override IEnumerable<string> AllKeys()
    {
        yield return nameof(UnixMs);
        yield return nameof(Username);
        yield return nameof(Action);
        yield return nameof(TargetId);
        yield return nameof(Context);
    }

    public override IEnumerable<string> UpdateableKeys() => EmptyKeys;

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        yield return new KeyValuePair<string, object>(nameof(UnixMs), UnixMs);
        yield return new KeyValuePair<string, object>(nameof(Username), Username);
        yield return new KeyValuePair<string, object>(nameof(Action), Action);
        yield return new KeyValuePair<string, object>(nameof(TargetId), TargetId);
        yield return new KeyValuePair<string, object>(nameof(Context), Context);
    }
}
