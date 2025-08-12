namespace Dxs.Bsv.Tokens;

public interface ITokenSchema
{
    string Name { get; }
    string TokenId { get; }
    string Symbol { get; }
    uint SatoshisPerToken { get; }

    public byte[] ToBytes();
    public string ToJson();
}