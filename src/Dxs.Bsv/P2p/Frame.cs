using System;

namespace Dxs.Bsv.P2p;

/// <summary>
/// A decoded Bitcoin P2P wire frame: 24-byte header (magic + command +
/// payload-length + checksum) followed by the payload bytes.
/// </summary>
public sealed record Frame(string Command, byte[] Payload)
{
    /// <summary>Header size in bytes for the basic (non-extmsg) frame format.</summary>
    public const int HeaderSize = 24;

    public const int MagicOffset      = 0;
    public const int CommandOffset    = 4;
    public const int LengthOffset     = 16;
    public const int ChecksumOffset   = 20;

    public const int MagicSize        = 4;
    public const int CommandSize      = 12;
    public const int LengthSize       = 4;
    public const int ChecksumSize     = 4;

    /// <summary>
    /// Soft default for a single received non-block payload (matches
    /// BSV-Node's <c>LEGACY_MAX_PROTOCOL_PAYLOAD_LENGTH</c>, 2 MiB).
    /// Negotiable upwards via <c>protoconf</c>.
    /// </summary>
    public const int LegacyMaxPayloadLength = 2 * 1024 * 1024;
}
