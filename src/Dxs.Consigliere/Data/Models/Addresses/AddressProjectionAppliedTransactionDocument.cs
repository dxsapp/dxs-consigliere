namespace Dxs.Consigliere.Data.Models.Addresses;

public sealed class AddressProjectionAppliedTransactionDocument
{
    public string Id { get; set; }
    public string TxId { get; set; }
    public string AppliedState { get; set; } = AddressProjectionApplicationState.None;
    public string ConfirmedBlockHash { get; set; }
    public AddressProjectionUtxoSnapshot[] Credits { get; set; } = [];
    public AddressProjectionUtxoSnapshot[] Debits { get; set; } = [];
    public DateTimeOffset? LastObservedAt { get; set; }
    public long LastSequence { get; set; }

    public static string GetId(string txId) => $"address/projection/applied/{txId}";
}
