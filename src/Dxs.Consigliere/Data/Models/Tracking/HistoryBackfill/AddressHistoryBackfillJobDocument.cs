namespace Dxs.Consigliere.Data.Models.Tracking.HistoryBackfill;

public sealed class AddressHistoryBackfillJobDocument : HistoryBackfillJobDocumentBase
{
    public string Address { get; set; }
    public AddressHistoryBackfillPayload Payload { get; set; } = new();

    public override string GetId() => GetId(Address);

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        foreach (var key in JobKeys())
            yield return key;

        yield return nameof(Address);
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

        yield return new(nameof(Address), Address);
        yield return new(nameof(Payload), Payload);
    }

    public static string GetId(string address) => $"history-backfill/address/{address}";
}
