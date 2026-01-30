namespace Dxs.Bsv.Models;

public readonly struct Payment(PrivateKey privateKey, OutPoint outPoint)
{
    public PrivateKey PrivateKey { get; } = privateKey;
    public OutPoint OutPoint { get; } = outPoint;
}
