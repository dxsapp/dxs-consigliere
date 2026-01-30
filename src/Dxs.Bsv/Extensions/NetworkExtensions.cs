using System;

namespace Dxs.Bsv.Extensions;

public static class NetworkExtensions
{
    public static NBitcoin.Network ToNBitcoin(this Network network)
        => network switch
        {
            Network.Mainnet => NBitcoin.Network.Main,
            Network.Testnet => NBitcoin.Network.TestNet,
            _ => throw new ArgumentOutOfRangeException(nameof(network), network, null)
        };
}
