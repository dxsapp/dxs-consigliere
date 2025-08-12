using Dxs.Bsv.Script;

namespace Dxs.Consigliere.Data.Models.History;

public class AddressHistory
{
    public string Address { get; set; }

    public string TokenId { get; set; }

    public string TxId { get; set; }

    public ScriptType ScriptType { get; set; }


    public long Timestamp { get; set; }
    public int Height { get; set; }

    public bool ValidStasTx { get; set; }

    public long SpentSatoshis { get; set; }
    public long ReceivedSatoshis { get; set; }
    public long BalanceSatoshis { get; set; }
    public long TxFeeSatoshis { get; set; }

    public string Note { get; set; }
    public int Side { get; set; }

    /// <summary>
    /// Other addresses.
    /// Applicable only for STAS transactions
    /// </summary>
    public HashSet<string> FromAddresses { get; set; }

    /// <summary>
    /// Other addresses.
    /// Applicable only for STAS transactions
    /// </summary>
    public HashSet<string> ToAddresses { get; set; }
}