namespace Dxs.Consigliere.Data.Models.Tracking;

public sealed class TrackedTokenStatusDocument : TrackedEntityStatusDocumentBase
{
    public string TokenId { get; set; }

    public override string GetId() => GetId(TokenId);

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        foreach (var key in TrackedKeys())
            yield return key;

        foreach (var key in StatusKeys())
            yield return key;

        yield return nameof(TokenId);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        foreach (var key in TrackedUpdateableKeys())
            yield return key;

        foreach (var key in StatusUpdateableKeys())
            yield return key;
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        foreach (var entry in TrackedEntries())
            yield return entry;

        foreach (var entry in StatusEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(TokenId), TokenId);
    }

    public static string GetId(string tokenId) => $"tracked/token/{tokenId}/status";
}
