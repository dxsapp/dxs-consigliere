using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Data.Models.Runtime;

public sealed class RealtimeSourcePolicyOverrideDocument : AuditableEntity
{
    public const string DocumentId = "operator/runtime/realtime-source-policy";

    public string PrimaryRealtimeSource { get; set; }
    public string BitailsTransport { get; set; }
    public string UpdatedBy { get; set; }

    public override string GetId() => DocumentId;

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        yield return nameof(PrimaryRealtimeSource);
        yield return nameof(BitailsTransport);
        yield return nameof(UpdatedBy);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        yield return nameof(PrimaryRealtimeSource);
        yield return nameof(BitailsTransport);
        yield return nameof(UpdatedBy);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(PrimaryRealtimeSource), PrimaryRealtimeSource);
        yield return new KeyValuePair<string, object>(nameof(BitailsTransport), BitailsTransport);
        yield return new KeyValuePair<string, object>(nameof(UpdatedBy), UpdatedBy);
    }
}
