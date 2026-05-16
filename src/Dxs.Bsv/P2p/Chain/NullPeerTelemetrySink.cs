#nullable enable
using System;
using System.Collections.Generic;

using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Session;

namespace Dxs.Bsv.P2p.Chain;

/// <summary>
/// No-op sink used as the default for every <see cref="PeerSession"/> in
/// W1. W6 swaps the construction site inside PeerManager when it ships
/// the production sink. Per-session instance (not a singleton) so that
/// W6 can substitute per-session without API change.
/// </summary>
public sealed class NullPeerTelemetrySink : IPeerTelemetrySink
{
    private static readonly IReadOnlyDictionary<RejectClass, long> EmptyRejects =
        new Dictionary<RejectClass, long>();

    public void RecordBytesIn(int n) { }
    public void RecordBytesOut(int n) { }
    public void RecordPingRtt(TimeSpan rtt) { }
    public void RecordGetDataRequested(InvType type, ReadOnlySpan<byte> hash) { }
    public void RecordGetDataServed(InvType type, ReadOnlySpan<byte> hash, TimeSpan serveLatency) { }
    public void RecordRelayBackInv(ReadOnlySpan<byte> txid) { }
    public void RecordRejectReceived(RejectClass cls) { }
    public void RecordProtocolViolation(string reason) { }
    public void RecordDisconnect(DisconnectReason reason) { }

    public PeerTelemetry Snapshot() => new(
        BytesIn: 0,
        BytesOut: 0,
        LastRecvUtc: null,
        LastSendUtc: null,
        PingRttP50Ms: 0,
        PingRttP95Ms: 0,
        PingSampleCount: 0,
        GetDataRequestedCount: 0,
        GetDataServedCount: 0,
        GetDataServeP50Ms: 0,
        GetDataServeP95Ms: 0,
        RelayBackInvCount: 0,
        RejectByClass: EmptyRejects,
        ProtocolViolationCount: 0,
        LastDisconnectReason: null);
}
