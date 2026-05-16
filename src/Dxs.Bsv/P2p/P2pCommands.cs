namespace Dxs.Bsv.P2p;

/// <summary>
/// Canonical Bitcoin P2P message command strings.
/// Each is ASCII, 12 bytes on the wire, NUL-padded.
/// </summary>
public static class P2pCommands
{
    public const int CommandSize = 12;

    // Handshake & keepalive
    public const string Version    = "version";
    public const string Verack     = "verack";
    public const string Ping       = "ping";
    public const string Pong       = "pong";

    // BSV-specific
    public const string Protoconf  = "protoconf";
    public const string Authch     = "authch";
    public const string Authresp   = "authresp";

    // Inventory & relay
    public const string Inv        = "inv";
    public const string GetData    = "getdata";
    public const string Tx         = "tx";
    public const string NotFound   = "notfound";
    public const string Reject     = "reject";

    // Addr-gossip
    public const string Addr       = "addr";
    public const string AddrV2     = "addrv2";
    public const string GetAddr    = "getaddr";

    // Headers / blocks (we mostly drop these, but parse for diagnostics)
    public const string Headers    = "headers";
    public const string GetHeaders = "getheaders";
    public const string GetBlocks  = "getblocks";
    public const string Block      = "block";
    public const string CmpctBlock = "cmpctblock";
    public const string BlockTxn   = "blocktxn";
    public const string MerkleBlock = "merkleblock";

    // Capability negotiation we accept but otherwise ignore
    public const string SendHeaders = "sendheaders";
    public const string SendCmpct   = "sendcmpct";
    public const string FeeFilter   = "feefilter";
    public const string SendHdrsEn  = "sendhdrsen";
    public const string Mempool     = "mempool";
}
