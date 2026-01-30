using Dxs.Bsv;
using Dxs.Consigliere.Configs;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.Impl;

public class NetworkProvider : INetworkProvider
{
    public Network Network { get; }

    public NetworkProvider(IOptions<NetworkConfig> config)
    {
        Network = config.Value.Network.ToLowerInvariant() switch
        {
            "testnet" => Network.Testnet,
            "mainnet" => Network.Mainnet,
            _ => throw new ArgumentException($"Invalid network configuration: {config.Value.Network}. Must be 'Mainnet' or 'Testnet'.")
        };
    }
}
