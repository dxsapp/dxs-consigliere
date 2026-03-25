namespace Dxs.Bsv.BitcoinMonitor.Models;

public readonly struct BlockMessage(string blockHash, string source)
{
    public string BlockHash { get; } = blockHash;
    public string Source { get; } = source;
}
