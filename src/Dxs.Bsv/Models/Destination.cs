
namespace Dxs.Bsv.Models;

public readonly struct Destination
{
    public Destination(Address address, ulong satoshis, byte[] data)
        => (Address, Satoshis, Data) = (address, satoshis, data);

    public Destination(Address address, ulong satoshis)
        => (Address, Satoshis, Data) = (address, satoshis, null);

    public Address Address { get; }
    public ulong Satoshis { get; }
    public byte[] Data { get; }

    public override string ToString()
        => $"!Destination! Address: \"{Address.Value}\"; Satoshis: {Satoshis}; Data: \"{Data.ToHexString()}\"";
}
