namespace Dxs.Infrastructure.Bitails.Dto;

public class TokenDetailsDto
{
    // curl "https://api.bitails.io/token/3c9ca41f66c5b20d6aecf198fe9c35b3c5bf51bc/symbol/ABC5"
    public string[] ContractTxs { get; set; } = [];
    public string[] IssuanceTxs { get; set; } = [];
    public string Name { get; set; }
    public string TokenId { get; set; }
    public string ProtocolId { get; set; }
    public string Symbol { get; set; }
    public string Description { get; set; }
    public string Image { get; set; }
    public decimal TotalSupply { get; set; }
    public int Decimals { get; set; }
    public int SatsPerToken { get; set; }
}
