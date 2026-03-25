namespace Dxs.Consigliere.Data.Models.Tracking;

public sealed class TrackedAddressDocument : TrackedEntityDocumentBase
{
    public string Address { get; set; }
    public string Name { get; set; }

    public override string GetId() => GetId(Address);

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        foreach (var key in TrackedKeys())
            yield return key;

        yield return nameof(Address);
        yield return nameof(Name);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        foreach (var key in TrackedUpdateableKeys())
            yield return key;

        yield return nameof(Name);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        foreach (var entry in TrackedEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(Address), Address);
        yield return new KeyValuePair<string, object>(nameof(Name), Name);
    }

    public static string GetId(string address) => $"tracked/address/{address}";
}
