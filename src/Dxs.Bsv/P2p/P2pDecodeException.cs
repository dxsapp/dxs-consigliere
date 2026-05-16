using System;

namespace Dxs.Bsv.P2p;

/// <summary>
/// Thrown when a P2P message or frame fails to decode because of malformed
/// or unexpected wire bytes. The network layer must catch this and translate
/// it into a peer-disconnect or banscore action; it must NOT bubble up to
/// the host process unhandled.
/// </summary>
public sealed class P2pDecodeException : Exception
{
    public P2pDecodeException(string message) : base(message) { }
    public P2pDecodeException(string message, Exception inner) : base(message, inner) { }
}
