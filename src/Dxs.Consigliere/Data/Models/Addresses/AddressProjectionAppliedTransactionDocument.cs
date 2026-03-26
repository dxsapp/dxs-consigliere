namespace Dxs.Consigliere.Data.Models.Addresses;

public sealed class AddressProjectionAppliedTransactionDocument
{
    public string Id { get; set; }
    public string TxId { get; set; }
    public string AppliedState { get; set; } = AddressProjectionApplicationState.None;
    public string ConfirmedBlockHash { get; set; }
    public AddressProjectionUtxoSnapshot[] Credits { get; set; } = [];
    public AddressProjectionUtxoSnapshot[] Debits { get; set; } = [];
    public long? Timestamp { get; set; }
    public int? Height { get; set; }
    public bool? ValidStasTx { get; set; }
    public long? TxFeeSatoshis { get; set; }
    public string Note { get; set; }
    public string[] FromAddresses { get; set; } = [];
    public string[] ToAddresses { get; set; } = [];
    public DateTimeOffset? LastObservedAt { get; set; }
    public long LastSequence { get; set; }

    public static string GetId(string txId) => $"address/projection/applied/{txId}";
}
