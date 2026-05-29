namespace Dxs.Infrastructure.Common;

public static class ExternalChainProviderName
{
    public const string Bitails = "bitails";
    public const string JungleBus = "junglebus";
    public const string WhatsOnChain = "whatsonchain";

    /// <summary>
    /// The in-house BSV P2P thin node. Distinct from <c>node</c>
    /// (a full bitcoin-sv RPC node): <c>p2p</c> observes the BSV
    /// P2P network directly and serves rawTx via <c>getdata</c>.
    /// Matches the <c>TxObservationSource.P2p</c> journal tag.
    /// </summary>
    public const string P2p = "p2p";
}
