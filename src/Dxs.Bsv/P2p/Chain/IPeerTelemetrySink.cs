#nullable enable
using System;

using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Session;

namespace Dxs.Bsv.P2p.Chain;

/// <summary>
/// Per-session telemetry sink. <see cref="PeerSession"/> calls the
/// rich-event methods directly from its send/receive loops, and the
/// relay coordinator (consigliere side) calls
/// <see cref="RecordGetDataRequested"/> / <see cref="RecordGetDataServed"/>
/// / <see cref="RecordRelayBackInv"/> on the same instance via
/// <c>session.Telemetry</c>.
///
/// Frozen by the Wave 1 contract freeze (see
/// docs/stream-tasks/bsv-headers-chain-wave/slices.md §S0.4).
/// W6 swaps the construction site (inside PeerManager) without changing
/// this interface; rich events preserve <see cref="RejectClass"/> and
/// per-hash timing so W6 can compute its own aggregates.
/// </summary>
public interface IPeerTelemetrySink
{
    void RecordBytesIn(int n);
    void RecordBytesOut(int n);
    void RecordPingRtt(TimeSpan rtt);
    void RecordGetDataRequested(InvType type, ReadOnlySpan<byte> hash);
    void RecordGetDataServed(InvType type, ReadOnlySpan<byte> hash, TimeSpan serveLatency);
    void RecordRelayBackInv(ReadOnlySpan<byte> txid);
    void RecordRejectReceived(RejectClass cls);
    void RecordProtocolViolation(string reason);
    void RecordDisconnect(DisconnectReason reason);
    PeerTelemetry Snapshot();
}
