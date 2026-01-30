namespace Dxs.Consigliere.Data.Transactions;

public readonly struct UtxoId
{
    public string TxId { get; init; }
    public int Vout { get; init; }

    public override string ToString() => $"{Vout}:{TxId}";
}
