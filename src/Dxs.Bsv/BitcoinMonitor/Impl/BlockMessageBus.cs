using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Dataflow;

namespace Dxs.Bsv.BitcoinMonitor.Impl;

public class BlockMessageBus : RxPubSub<BlockMessage>, IBlockMessageBus;
