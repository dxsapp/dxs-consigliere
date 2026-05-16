using System;
using System.Collections.Generic;

namespace Dxs.Bsv.P2p;

/// <summary>
/// BSV P2P network identification — magic bytes on the wire, default port,
/// and DNS seeds. Mainnet only in Phase 1.
/// </summary>
public sealed class P2pNetwork
{
    /// <summary>
    /// BSV mainnet. Magic bytes on the wire: <c>e3 e1 f3 e8</c>
    /// (matches pchMessageStart in bitcoin-sv/src/chainparams.cpp).
    ///
    /// IMPORTANT: the uint32 constant is stored in a form such that
    /// <see cref="System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian"/>
    /// produces the correct wire bytes [e3, e1, f3, e8].
    ///   WriteUInt32LE(0xE8F3E1E3) → bytes [e3, e1, f3, e8] ✓
    ///   ReadUInt32LE([e3, e1, f3, e8]) → 0xE8F3E1E3 ✓
    /// </summary>
    public static P2pNetwork Mainnet { get; } = new(
        name: "mainnet",
        magic: 0xE8F3E1E3u,   // ← byte-swapped so LE writes produce e3 e1 f3 e8 on wire
        defaultPort: 8333,
        dnsSeeds: new[]
        {
            "seed.bitcoinsv.io",
            "seed.satoshisvision.network",
            "seed.bitcoinseed.directory",
        },
        // Hardcoded fallback seeds — mirror of bitcoin-sv's pnSeed6_main[] in chainparams.cpp.
        // These are public BSV nodes verified to accept random inbound. Used when DNS-seed
        // crawlers return nodes that have us banned, so we still have a bootstrap path.
        fallbackSeeds: new[]
        {
            "198.154.93.212:8333",
            "185.38.149.12:8333",
            "57.128.216.248:8333",
            "135.181.137.155:8333",
            "95.217.204.168:8333",
            "37.27.131.85:8333",
            "162.19.138.6:8333",
            "193.145.14.195:8333",
        });

    private P2pNetwork(string name, uint magic, ushort defaultPort, string[] dnsSeeds, string[] fallbackSeeds)
    {
        Name = name;
        Magic = magic;
        DefaultPort = defaultPort;
        DnsSeeds = dnsSeeds;
        FallbackSeeds = fallbackSeeds;
    }

    public string Name { get; }

    /// <summary>
    /// Network-magic stored so that
    /// <see cref="System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian"/>
    /// produces the correct 4 on-wire bytes.
    /// </summary>
    public uint Magic { get; }

    public ushort DefaultPort { get; }

    public IReadOnlyList<string> DnsSeeds { get; }

    /// <summary>
    /// Hardcoded fallback seed peers in <c>host:port</c> form.
    /// Used when DNS-seed crawlers return nodes that won't peer with us
    /// (e.g. accumulated bans, operator whitelist), giving a still-working
    /// bootstrap path so addr-gossip can begin.
    /// </summary>
    public IReadOnlyList<string> FallbackSeeds { get; }

    /// <summary>The 4 on-wire magic bytes: [e3, e1, f3, e8].</summary>
    public byte[] MagicBytes
    {
        get
        {
            var bytes = new byte[4];
            bytes[0] = (byte)(Magic & 0xFF);
            bytes[1] = (byte)((Magic >> 8) & 0xFF);
            bytes[2] = (byte)((Magic >> 16) & 0xFF);
            bytes[3] = (byte)((Magic >> 24) & 0xFF);
            return bytes;
        }
    }
}
