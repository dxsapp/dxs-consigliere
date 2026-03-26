namespace Dxs.Consigliere.Dto.Responses;

public class AddressStateResponse
{
    public string Address { get; set; }
    public BalanceDto[] Balances { get; set; } = [];
    public UtxoDto[] UtxoSet { get; set; } = [];
}
