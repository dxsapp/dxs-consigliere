using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Data.Models.Runtime;

public sealed class SetupBootstrapDocument : AuditableEntity
{
    public const string DocumentId = "operator/runtime/setup-bootstrap";

    public bool SetupCompleted { get; set; }
    public bool AdminEnabled { get; set; }
    public string AdminUsername { get; set; }
    public string AdminPasswordHash { get; set; }
    public string UpdatedBy { get; set; }

    public override string GetId() => DocumentId;

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        yield return nameof(SetupCompleted);
        yield return nameof(AdminEnabled);
        yield return nameof(AdminUsername);
        yield return nameof(AdminPasswordHash);
        yield return nameof(UpdatedBy);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        yield return nameof(SetupCompleted);
        yield return nameof(AdminEnabled);
        yield return nameof(AdminUsername);
        yield return nameof(AdminPasswordHash);
        yield return nameof(UpdatedBy);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(SetupCompleted), SetupCompleted);
        yield return new KeyValuePair<string, object>(nameof(AdminEnabled), AdminEnabled);
        yield return new KeyValuePair<string, object>(nameof(AdminUsername), AdminUsername);
        yield return new KeyValuePair<string, object>(nameof(AdminPasswordHash), AdminPasswordHash);
        yield return new KeyValuePair<string, object>(nameof(UpdatedBy), UpdatedBy);
    }
}
