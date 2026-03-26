namespace Dxs.Consigliere.Data.Models.Transactions;

public sealed class TokenValidationDependentsDocument
{
    public string Id { get; set; }
    public string TxId { get; set; }
    public string[] DirectDependents { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }

    public static string GetId(string txId) => $"tx/validation/dependents/{txId}";
}
