#nullable enable
using System;
using System.Collections.Generic;

using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Session;

namespace Dxs.Bsv.P2p.Chain;

/// <summary>
/// Point-in-time snapshot of one <see cref="PeerSession"/>'s aggregate
/// telemetry counters. Returned by <see cref="IPeerTelemetrySink.Snapshot"/>.
/// Field set is frozen by the Wave 1 contract freeze (see
/// docs/stream-tasks/bsv-headers-chain-wave/slices.md §S0.3).
/// Downstream waves W4 (source metrics) and W6 (peer scoring) read this
/// shape; changing it requires a contract-freeze amendment slice.
/// </summary>
public sealed record PeerTelemetry(
    long BytesIn,
    long BytesOut,
    DateTimeOffset? LastRecvUtc,
    DateTimeOffset? LastSendUtc,
    double PingRttP50Ms,
    double PingRttP95Ms,
    int PingSampleCount,
    long GetDataRequestedCount,
    long GetDataServedCount,
    double GetDataServeP50Ms,
    double GetDataServeP95Ms,
    long RelayBackInvCount,
    IReadOnlyDictionary<RejectClass, long> RejectByClass,
    long ProtocolViolationCount,
    DisconnectReason? LastDisconnectReason);
