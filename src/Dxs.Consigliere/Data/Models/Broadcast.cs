namespace Dxs.Consigliere.Data.Models;

public class Broadcast : AuditableEntity
{
    public Broadcast() : base() { }

    public string TxId { get; set; }
    public bool Success { get; set; }
    public string Code { get; set; }
    public string Message { get; set; }
    public string BatchId { get; set; }

    public override string GetId() => $"broadcast/{TxId}/{CreatedAt}";

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
        {
            yield return key;
        }

        yield return nameof(TxId);
        yield return nameof(Success);
        yield return nameof(Code);
        yield return nameof(Message);
        yield return nameof(BatchId);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        yield return nameof(Success);
        yield return nameof(Code);
        yield return nameof(Message);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(TxId), TxId);
        yield return new KeyValuePair<string, object>(nameof(Success), Success);
        yield return new KeyValuePair<string, object>(nameof(Code), Code);
        yield return new KeyValuePair<string, object>(nameof(Message), Message);
        yield return new KeyValuePair<string, object>(nameof(BatchId), BatchId);
    }
}
