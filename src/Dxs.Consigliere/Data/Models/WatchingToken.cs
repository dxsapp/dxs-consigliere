namespace Dxs.Consigliere.Data.Models;

public class WatchingToken: AuditableEntity
{
    public string TokenId { get; init; }
    public string Symbol { get; init; }
    
    public override string GetId() => $"token/{TokenId}/{Symbol}";

    
    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
        {
            yield return key;
        }

        yield return nameof(TokenId);
        yield return nameof(Symbol);
    }
    
    public override IList<string> UpdateableKeys() => EmptyKeys;
    
    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(TokenId), TokenId);
        yield return new KeyValuePair<string, object>(nameof(Symbol), Symbol);
    }
}