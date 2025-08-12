namespace Dxs.Consigliere.Data.Models;

public class WatchingAddress: AuditableEntity
{
    public string Name { get; init; }
    public string Address { get; init; }

    public override string GetId() => $"address/{Address}";

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
        {
            yield return key;
        }

        yield return nameof(Address);
        yield return nameof(Name);
    }
    
    public override IList<string> UpdateableKeys() => EmptyKeys;
    
    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;
        
        yield return new KeyValuePair<string, object>(nameof(Address), Address);
        yield return new KeyValuePair<string, object>(nameof(Name), Name);
    }
}