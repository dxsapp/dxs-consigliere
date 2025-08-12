using Dxs.Consigliere.Data.Models.History;

namespace Dxs.Consigliere.Dto;

public class AddressHistoryDto
{
    public string Address { get; set; }
    public string TokenId { get; set; }

    public string TxId { get; set; }


    public long Timestamp { get; set; }
    public int Height { get; set; }

    public bool ValidStasTx { get; set; }

    public long SpentSatoshis { get; set; }
    public long ReceivedSatoshis { get; set; }
    public long BalanceSatoshis { get; set; }
    public long TxFeeSatoshis { get; set; }
    public string Note { get; set; }

    /// <summary>
    /// Other addresses.
    /// Applicable only for STAS transactions
    /// </summary>
    public IEnumerable<string> FromAddresses { get; set; }

    /// <summary>
    /// Other addresses.
    /// Applicable only for STAS transactions
    /// </summary>
    public IEnumerable<string> ToAddresses { get; set; }

    public static AddressHistoryDto From(AddressHistory addressHistory)
        => new()
        {
            Address = addressHistory.Address,
            TokenId = addressHistory.TokenId,
            TxId = addressHistory.TxId,
            Timestamp = addressHistory.Timestamp,
            Height = addressHistory.Height,
            ValidStasTx = addressHistory.ValidStasTx,
            SpentSatoshis = addressHistory.SpentSatoshis,
            ReceivedSatoshis = addressHistory.ReceivedSatoshis,
            BalanceSatoshis = addressHistory.BalanceSatoshis,
            TxFeeSatoshis = addressHistory.TxFeeSatoshis,
            Note = addressHistory.Note,
            FromAddresses = addressHistory.FromAddresses?.Take(10),
            ToAddresses = addressHistory.ToAddresses?.Take(10)
        };
}