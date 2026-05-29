using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Data.Models.Runtime;

/// <summary>
/// Operator-tunable runtime settings, seeded from config on first write
/// and DB-authoritative thereafter (wizard-enabled-p2p-runtime-toggle
/// wave). The nucleus of a broader "runtime settings in the DB" model —
/// holds operational toggles ONLY, never secrets (those stay in the
/// secrets file, wave-A2/A3 decision).
///
/// <para>Read semantics live in <c>OperatorRuntimeSettingsService</c>:
/// <c>effective = doc?.Field ?? configSeed</c>. A null field means "not
/// overridden — use the config default".</para>
/// </summary>
public sealed class OperatorRuntimeSettingsDocument : AuditableEntity
{
    public const string DocumentId = "operator/runtime/operator-settings";

    /// <summary>
    /// Whether the BSV P2P thin-node subsystem should run. Null = not
    /// overridden (fall back to <c>BsvP2pConfig.Enabled</c>). The wizard
    /// sets this true on completion; a runtime change is picked up live by
    /// the P2P hosted service via the RavenDB Changes API.
    /// </summary>
    public bool? P2pEnabled { get; set; }

    public string UpdatedBy { get; set; }

    public override string GetId() => DocumentId;

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        yield return nameof(P2pEnabled);
        yield return nameof(UpdatedBy);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        yield return nameof(P2pEnabled);
        yield return nameof(UpdatedBy);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(P2pEnabled), P2pEnabled);
        yield return new KeyValuePair<string, object>(nameof(UpdatedBy), UpdatedBy);
    }
}
