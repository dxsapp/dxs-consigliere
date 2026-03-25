namespace Dxs.Consigliere.Data.Models.Tracking;

public sealed class TrackedTokenDocument : TrackedEntityDocumentBase
{
    public string TokenId { get; set; }
    public string Symbol { get; set; }

    public override string GetId() => GetId(TokenId);

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        foreach (var key in TrackedKeys())
            yield return key;

        yield return nameof(TokenId);
        yield return nameof(Symbol);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        foreach (var key in TrackedUpdateableKeys())
            yield return key;

        yield return nameof(Symbol);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        foreach (var entry in TrackedEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(TokenId), TokenId);
        yield return new KeyValuePair<string, object>(nameof(Symbol), Symbol);
    }

    public static string GetId(string tokenId) => $"tracked/token/{tokenId}";
}
