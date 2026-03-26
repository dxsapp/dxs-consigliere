namespace Dxs.Consigliere.Data.Models.Transactions;

public sealed class TokenValidationDependencyDocument
{
    public string Id { get; set; }
    public string TxId { get; set; }
    public string[] DependsOnTxIds { get; set; } = [];
    public string[] MissingDependencies { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }

    public static string GetId(string txId) => $"tx/validation/dependencies/{txId}";
}
