namespace Dxs.Consigliere.Data.Models.History;

public class TransactionRef(string transactionId): Entity
{
    public string TransactionId { get; set; } = transactionId;
    public string Note { get; set; }

    public override string GetId() => GetId(TransactionId);

    public override IEnumerable<string> AllKeys()
    {
        yield return nameof(TransactionId);
        yield return nameof(Note);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        yield return nameof(Note);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        yield return new KeyValuePair<string, object>(nameof(TransactionId), TransactionId);
        yield return new KeyValuePair<string, object>(nameof(Note), Note);
    }

    public static string GetId(string transactionId) => $"ref/{transactionId}";
}