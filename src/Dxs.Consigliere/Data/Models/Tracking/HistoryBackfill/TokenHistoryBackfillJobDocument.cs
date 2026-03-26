namespace Dxs.Consigliere.Data.Models.Tracking.HistoryBackfill;

public sealed class TokenHistoryBackfillJobDocument : HistoryBackfillJobDocumentBase
{
    public string TokenId { get; set; }
    public TokenHistoryBackfillPayload Payload { get; set; } = new();

    public override string GetId() => GetId(TokenId);

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        foreach (var key in JobKeys())
            yield return key;

        yield return nameof(TokenId);
        yield return nameof(Payload);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        foreach (var key in JobKeys())
            yield return key;

        yield return nameof(Payload);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        foreach (var entry in JobEntries())
            yield return entry;

        yield return new(nameof(TokenId), TokenId);
        yield return new(nameof(Payload), Payload);
    }

    public static string GetId(string tokenId) => $"history-backfill/token/{tokenId}";
}
