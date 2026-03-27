using System;

namespace Dxs.Bsv.Zmq;

[Flags]
public enum ZmqSubscriptionTopics
{
    None = 0,
    Mempool = 1,
    Blocks = 2,
    All = Mempool | Blocks
}
