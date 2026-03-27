using System;

namespace Dxs.Infrastructure.Bitails.Realtime;

public sealed record BitailsRealtimeTransactionNotification(string Topic, string TxId, DateTimeOffset ObservedAt);
