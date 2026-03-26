namespace Dxs.Consigliere.Data.Models.Tracking;

public sealed class TrackedTokenDocument : TrackedEntityDocumentBase
{
    public string TokenId { get; set; }
    public string Symbol { get; set; }
    public TrackedTokenHistorySecurityState HistorySecurity { get; set; } = new();

    public override string GetId() => GetId(TokenId);

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        foreach (var key in TrackedKeys())
            yield return key;

        yield return nameof(TokenId);
        yield return nameof(Symbol);
        yield return nameof(HistorySecurity);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        foreach (var key in TrackedUpdateableKeys())
            yield return key;

        yield return nameof(Symbol);
        yield return nameof(HistorySecurity);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        foreach (var entry in TrackedEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(TokenId), TokenId);
        yield return new KeyValuePair<string, object>(nameof(Symbol), Symbol);
        yield return new KeyValuePair<string, object>(nameof(HistorySecurity), HistorySecurity);
    }

    public static string GetId(string tokenId) => $"tracked/token/{tokenId}";
}
