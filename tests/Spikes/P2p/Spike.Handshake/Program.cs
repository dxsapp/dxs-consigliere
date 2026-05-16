// SPIKE — disposable. Validates BSV P2P handshake against public peers.
//
// Wire format reference: https://en.bitcoin.it/wiki/Protocol_documentation
// BSV mainnet network magic: 0xE3E1F3E8 (little-endian on wire: E3 E1 F3 E8)

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Spike.Handshake;

internal static class Program
{
    // BSV mainnet peers known to accept inbound (from WoC peer/info, fresh).
    private static readonly (string Host, int Port)[] Peers =
    {
        ("57.128.233.172",  8333),
        ("195.144.22.198",  8333),
        ("135.125.170.182", 8333),
        ("162.19.138.6",    8333),
        ("99.127.49.102",   8333),
        ("65.108.102.125",  8333),
        ("198.154.93.210",  8333),
        ("65.109.56.244",   8333),
    };

    private const uint MainnetMagic = 0xE3E1F3E8;
    private const int  ProtocolVersion = 70016;
    private const string UserAgent = "/Bitcoin SV:1.2.1/";
    private const ulong OurServices = 0x21; // NODE_NETWORK | NODE_BITCOIN_CASH
    private const int  StartHeight = 950000;

    private static async Task<int> Main(string[] args)
    {
        var verboseMode = args.Contains("--verbose") || args.Contains("-v");
        Console.WriteLine($"P2P handshake spike — {Peers.Length} peers, magic 0x{MainnetMagic:X8}, version {ProtocolVersion}");
        Console.WriteLine(new string('-', 100));

        if (verboseMode)
        {
            // Just probe the first peer with full byte logging.
            await DebugOnePeerAsync(Peers[0].Host, Peers[0].Port);
            return 0;
        }

        // Sequential to avoid triggering DOS protection on shared NATs / subnets.
        var results = new List<HandshakeResult>();
        foreach (var p in Peers)
        {
            var r = await HandshakeAsync(p.Host, p.Port);
            results.Add(r);
            Console.WriteLine($"  {p.Host}:{p.Port} -> {(r.Ok ? "OK" : "FAIL")}  {r.Reason}");
            await Task.Delay(500);
        }

        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"{"PEER",-25} {"OK",-4} {"HSHAKE",-9} {"PING",-9} {"VER",-6} {"USER-AGENT",-35} REASON");
        Console.WriteLine(new string('-', 100));
        foreach (var r in results)
        {
            var peer = $"{r.Host}:{r.Port}";
            var hs   = r.HandshakeMs.HasValue ? $"{r.HandshakeMs}ms" : "-";
            var pg   = r.PingMs.HasValue      ? $"{r.PingMs}ms"      : "-";
            var ok   = r.Ok ? "yes" : "no";
            Console.WriteLine($"{peer,-25} {ok,-4} {hs,-9} {pg,-9} {r.PeerVersion?.ToString() ?? "-",-6} {Trunc(r.UserAgent ?? "-", 35),-35} {r.Reason}");
        }

        var good = results.Count(r => r.Ok);
        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"Result: {good}/{results.Count} peers fully handshaked.");
        return good > 0 ? 0 : 1;
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    private static async Task DebugOnePeerAsync(string host, int port)
    {
        Console.WriteLine($"[debug] connecting to {host}:{port}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, cts.Token);
        var stream = tcp.GetStream();
        Console.WriteLine($"[debug] connected, waiting 3s to see if peer sends version first...");

        // Passive wait — does peer initiate?
        var passiveBuf = new byte[4096];
        using (var passiveCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token))
        {
            passiveCts.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                while (true)
                {
                    var n = await stream.ReadAsync(passiveBuf, passiveCts.Token);
                    if (n == 0) { Console.WriteLine($"[debug] peer closed during passive wait"); return; }
                    Console.WriteLine($"[debug] passive +{n} bytes: {Hex(passiveBuf.AsSpan(0, Math.Min(n, 80)))}{(n > 80 ? "…" : "")}");
                    TryDecodeMessages(passiveBuf.Take(n).ToArray());
                }
            }
            catch (OperationCanceledException) when (!cts.IsCancellationRequested)
            {
                Console.WriteLine($"[debug] passive wait done, no bytes from peer. Sending our version...");
            }
        }

        // Send our version.
        var payload = BuildVersionPayload(host, port);
        var versionFrame = BuildFrame("version", payload);
        Console.WriteLine($"[debug] sending version ({versionFrame.Length} bytes): {Hex(versionFrame[..Math.Min(80, versionFrame.Length)])}…");
        await stream.WriteAsync(versionFrame, cts.Token);

        // Now read all bytes until close, with timeout.
        var buf = new byte[4096];
        var allRead = new MemoryStream();
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var n = await stream.ReadAsync(buf, cts.Token);
                if (n == 0)
                {
                    Console.WriteLine($"[debug] peer closed after receiving {allRead.Length} bytes total");
                    break;
                }
                Console.WriteLine($"[debug] +{n} bytes: {Hex(buf.AsSpan(0, Math.Min(n, 80)))}{(n > 80 ? "…" : "")}");
                allRead.Write(buf, 0, n);
                TryDecodeMessages(allRead.ToArray());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[debug] read error: {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine($"[debug] total bytes received: {allRead.Length}");
        if (allRead.Length > 0) Console.WriteLine($"[debug] full dump: {Hex(allRead.ToArray())}");
    }

    private static byte[] BuildFrame(string command, byte[] payload)
    {
        var header = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), MainnetMagic);
        var cmd = Encoding.ASCII.GetBytes(command);
        Array.Copy(cmd, 0, header, 4, cmd.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16, 4), (uint)payload.Length);
        Span<byte> ck = stackalloc byte[32];
        DoubleSha256(payload, ck);
        ck[..4].CopyTo(header.AsSpan(20, 4));
        var frame = new byte[24 + payload.Length];
        header.CopyTo(frame, 0);
        payload.CopyTo(frame, 24);
        return frame;
    }

    private static void TryDecodeMessages(byte[] data)
    {
        var off = 0;
        while (off + 24 <= data.Length)
        {
            var magic = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off, 4));
            var cmd = Encoding.ASCII.GetString(data, off + 4, 12).TrimEnd('\0');
            var len = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 16, 4));
            if (off + 24 + len > data.Length) return;
            Console.WriteLine($"[decode] magic=0x{magic:X8} cmd='{cmd}' len={len}");
            if (cmd == "version" && len >= 20)
            {
                var p = data.AsSpan(off + 24, (int)len).ToArray();
                var v = BinaryPrimitives.ReadInt32LittleEndian(p);
                Console.WriteLine($"[decode]   peer version={v}");
            }
            off += 24 + (int)len;
        }
    }

    private static string Hex(ReadOnlySpan<byte> b)
    {
        var sb = new StringBuilder(b.Length * 3);
        foreach (var x in b) sb.Append(x.ToString("x2")).Append(' ');
        return sb.ToString().TrimEnd();
    }

    private static async Task<HandshakeResult> HandshakeAsync(string host, int port)
    {
        var r = new HandshakeResult { Host = host, Port = port };
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var tcp = new TcpClient();

            var swTotal = Stopwatch.StartNew();
            await tcp.ConnectAsync(host, port, cts.Token);
            tcp.NoDelay = true;
            await using var stream = tcp.GetStream();
            var connectMs = (int)swTotal.ElapsedMilliseconds;

            // Send our version IMMEDIATELY in one syscall to avoid any "silent client" filtering.
            var versionPayload = BuildVersionPayload(host, port);
            await SendMessageAsync(stream, "version", versionPayload, cts.Token);

            // Expect: peer's version, peer's verack.
            string? peerUA = null;
            int? peerVersion = null;
            bool gotPeerVersion = false, gotPeerVerack = false;

            while (!(gotPeerVersion && gotPeerVerack))
            {
                var msg = await ReadMessageAsync(stream, cts.Token);
                switch (msg.Command)
                {
                    case "version":
                        (peerVersion, peerUA) = ParseVersion(msg.Payload);
                        gotPeerVersion = true;
                        // Reply with verack as soon as we see their version.
                        await SendMessageAsync(stream, "verack", Array.Empty<byte>(), cts.Token);
                        break;
                    case "verack":
                        gotPeerVerack = true;
                        break;
                    case "reject":
                        r.Reason = $"reject during handshake: {ParseRejectReason(msg.Payload)}";
                        return r;
                    // ignore everything else (sendheaders, sendcmpct, feefilter, addr, ping...)
                    case "ping":
                        // Peer pinging us mid-handshake — reply.
                        await SendMessageAsync(stream, "pong", msg.Payload, cts.Token);
                        break;
                    default:
                        break;
                }
            }

            r.HandshakeMs = (int)swTotal.ElapsedMilliseconds;
            r.PeerVersion = peerVersion;
            r.UserAgent   = peerUA;

            // Send ping, measure RTT.
            var nonce = new byte[8];
            RandomNumberGenerator.Fill(nonce);
            var swPing = Stopwatch.StartNew();
            await SendMessageAsync(stream, "ping", nonce, cts.Token);

            while (true)
            {
                var msg = await ReadMessageAsync(stream, cts.Token);
                if (msg.Command == "pong" && msg.Payload.AsSpan().SequenceEqual(nonce))
                {
                    r.PingMs = (int)swPing.ElapsedMilliseconds;
                    break;
                }
                if (msg.Command == "ping")
                {
                    await SendMessageAsync(stream, "pong", msg.Payload, cts.Token);
                }
                // otherwise ignore
            }

            r.Ok = true;
            r.Reason = $"connect {connectMs}ms";
            return r;
        }
        catch (OperationCanceledException)
        {
            r.Reason = "timeout";
            return r;
        }
        catch (Exception ex)
        {
            r.Reason = ex.GetType().Name + ": " + ex.Message;
            return r;
        }
    }

    private record struct Message(string Command, byte[] Payload);

    private static async Task SendMessageAsync(NetworkStream s, string command, byte[] payload, CancellationToken ct)
    {
        var header = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), MainnetMagic);
        var cmdBytes = Encoding.ASCII.GetBytes(command);
        Array.Copy(cmdBytes, 0, header, 4, cmdBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16, 4), (uint)payload.Length);

        Span<byte> checksum = stackalloc byte[32];
        DoubleSha256(payload, checksum);
        checksum[..4].CopyTo(header.AsSpan(20, 4));

        await s.WriteAsync(header, ct);
        if (payload.Length > 0)
            await s.WriteAsync(payload, ct);
        await s.FlushAsync(ct);
    }

    private static async Task<Message> ReadMessageAsync(NetworkStream s, CancellationToken ct)
    {
        var header = new byte[24];
        await ReadExactAsync(s, header, ct);

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
        if (magic != MainnetMagic)
            throw new InvalidDataException($"bad magic 0x{magic:X8} (expected 0x{MainnetMagic:X8})");

        var cmd = Encoding.ASCII.GetString(header, 4, 12).TrimEnd('\0');
        var len = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16, 4));
        if (len > 32 * 1024 * 1024)
            throw new InvalidDataException($"payload too large: {len}");

        var payload = new byte[len];
        if (len > 0) await ReadExactAsync(s, payload, ct);

        Span<byte> ck = stackalloc byte[32];
        DoubleSha256(payload, ck);
        if (!ck[..4].SequenceEqual(header.AsSpan(20, 4)))
            throw new InvalidDataException($"bad checksum on '{cmd}'");

        return new Message(cmd, payload);
    }

    private static async Task ReadExactAsync(NetworkStream s, byte[] buf, CancellationToken ct)
    {
        var off = 0;
        while (off < buf.Length)
        {
            var n = await s.ReadAsync(buf.AsMemory(off), ct);
            if (n == 0) throw new EndOfStreamException("peer closed");
            off += n;
        }
    }

    private static byte[] BuildVersionPayload(string remoteHost, int remotePort)
    {
        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms);

        w.Write((int)ProtocolVersion);                              // version
        w.Write((ulong)OurServices);                                // services NODE_NETWORK
        w.Write(DateTimeOffset.UtcNow.ToUnixTimeSeconds());         // timestamp
        WriteNetAddr(w, services: 1, host: remoteHost, port: remotePort); // addr_recv
        WriteNetAddr(w, services: OurServices, host: "0.0.0.0", port: 8333); // addr_from
        var nonce = new byte[8]; RandomNumberGenerator.Fill(nonce);
        w.Write(nonce);                                             // nonce
        WriteVarStr(w, UserAgent);                                  // user_agent
        w.Write((int)StartHeight);                                  // start_height
        w.Write((byte)1);                                           // relay = true
        return ms.ToArray();
    }

    private static void WriteNetAddr(BinaryWriter w, ulong services, string host, int port)
    {
        w.Write(services);
        Span<byte> ipv6 = stackalloc byte[16];
        ipv6[10] = 0xff; ipv6[11] = 0xff;
        if (IPAddress.TryParse(host, out var addr))
        {
            var v4 = addr.MapToIPv4().GetAddressBytes();
            v4.CopyTo(ipv6[12..]);
        }
        w.Write(ipv6);
        // port is BIG-ENDIAN, the only field that is.
        w.Write((byte)((port >> 8) & 0xFF));
        w.Write((byte)(port & 0xFF));
    }

    private static void WriteVarStr(BinaryWriter w, string s)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        WriteVarInt(w, (ulong)bytes.Length);
        w.Write(bytes);
    }

    private static void WriteVarInt(BinaryWriter w, ulong v)
    {
        if (v < 0xFD)             { w.Write((byte)v); }
        else if (v <= 0xFFFF)     { w.Write((byte)0xFD); w.Write((ushort)v); }
        else if (v <= 0xFFFFFFFF) { w.Write((byte)0xFE); w.Write((uint)v); }
        else                      { w.Write((byte)0xFF); w.Write(v); }
    }

    private static (int Version, string UserAgent) ParseVersion(byte[] p)
    {
        var br = new BinaryReader(new MemoryStream(p));
        var ver = br.ReadInt32();
        br.ReadUInt64();      // services
        br.ReadInt64();       // timestamp
        br.ReadBytes(26);     // addr_recv
        if (p.Length <= 4 + 8 + 8 + 26) return (ver, "");
        br.ReadBytes(26);     // addr_from
        br.ReadUInt64();      // nonce
        var ua = ReadVarStr(br);
        return (ver, ua);
    }

    private static string ParseRejectReason(byte[] p)
    {
        try
        {
            var br = new BinaryReader(new MemoryStream(p));
            var msg = ReadVarStr(br);
            var code = br.ReadByte();
            var reason = ReadVarStr(br);
            return $"msg={msg} code=0x{code:X2} reason={reason}";
        }
        catch { return BitConverter.ToString(p); }
    }

    private static string ReadVarStr(BinaryReader br)
    {
        var len = (int)ReadVarInt(br);
        return Encoding.ASCII.GetString(br.ReadBytes(len));
    }

    private static ulong ReadVarInt(BinaryReader br)
    {
        var b = br.ReadByte();
        return b switch
        {
            0xFD => br.ReadUInt16(),
            0xFE => br.ReadUInt32(),
            0xFF => br.ReadUInt64(),
            _ => b,
        };
    }

    private static void DoubleSha256(ReadOnlySpan<byte> input, Span<byte> output32)
    {
        Span<byte> tmp = stackalloc byte[32];
        SHA256.HashData(input, tmp);
        SHA256.HashData(tmp, output32);
    }

    private sealed class HandshakeResult
    {
        public string Host = "";
        public int Port;
        public bool Ok;
        public int? HandshakeMs;
        public int? PingMs;
        public int? PeerVersion;
        public string? UserAgent;
        public string Reason = "";
    }
}
