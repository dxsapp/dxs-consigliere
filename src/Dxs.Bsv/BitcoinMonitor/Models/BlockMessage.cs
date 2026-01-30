namespace Dxs.Bsv.BitcoinMonitor.Models;

public readonly struct BlockMessage(string blockHash)
{
    public string BlockHash { get; } = blockHash;
}
