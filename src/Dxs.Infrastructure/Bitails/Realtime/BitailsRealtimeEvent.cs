using System;

namespace Dxs.Infrastructure.Bitails.Realtime;

public sealed record BitailsRealtimeEvent(
    BitailsRealtimeEventKind Kind,
    string Topic,
    DateTimeOffset ObservedAt,
    string TxId = null,
    byte[] RawTransaction = null,
    string RemoveReason = null,
    string CollidedWithTransaction = null,
    string BlockHash = null
);
