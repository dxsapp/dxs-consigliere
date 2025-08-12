namespace Dxs.Consigliere.Data.Models;

public abstract class Entity
{
    protected static readonly IList<string> EmptyKeys = new List<string>();

    public string Id { get; set; }

    public abstract string GetId();
    public abstract IEnumerable<string> AllKeys();
    public abstract IEnumerable<string> UpdateableKeys();
    public abstract IEnumerable<KeyValuePair<string, object>> ToEntries();
}

public abstract class AuditableEntity: Entity
{
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? UpdatedAt { get; set; }

    public void SetUpdate() => UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public override IEnumerable<string> AllKeys()
    {
        yield return nameof(CreatedAt);
        yield return nameof(UpdatedAt);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        yield return nameof(UpdatedAt);
    }
    
    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        yield return new KeyValuePair<string, object>(nameof(UpdatedAt), UpdatedAt);
        yield return new KeyValuePair<string, object>(nameof(CreatedAt), CreatedAt);
    }
}