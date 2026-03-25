using System;

namespace Dxs.Bsv.BitcoinMonitor.Models;

public static class BlockObservationEventType
{
    public const string Connected = "block_connected";
    public const string Disconnected = "block_disconnected";
}

public sealed record BlockObservation(
    string EventType,
    string Source,
    string BlockHash,
    DateTimeOffset? ObservedAt = null,
    string Reason = null
);
