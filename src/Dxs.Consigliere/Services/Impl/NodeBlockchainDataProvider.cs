using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Block;
using Dxs.Bsv.Rpc.Services;
using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Services.Impl;

public class NodeBlockchainDataProvider(
    IRpcClient rpcClient,
    ITxMessageBus txMessageBus,
    INetworkProvider networkProvider,
    ILogger<NodeBlockchainDataProvider> logger
)
    : IBlockDataProvider
{
    public async Task<int> ProcessBlock(BlockProcessContext context, CancellationToken cancellationToken)
    {
        using var _ = logger.BeginScope("NodeBlockchainDataProvider.ProcessBlock: {Height}", context.Height);

        var blockStream = await rpcClient.GetBlockAsStream(context.Id);

        using var blockReader = BlockReader.Parse(blockStream, networkProvider.Network);

        logger.LogDebug("Start parse block");

        var count = 0;
        var logProgress = context.TransactionsCount > 10_000;
        var logStep = context.TransactionsCount / 20;

        foreach (var transaction in blockReader.Transactions())
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var message = TxMessage.FoundInBlock(
                transaction,
                context.Timestamp,
                context.Id,
                context.Height,
                count
            );
            txMessageBus.Post(message);

            count++;

            if (logProgress && count % logStep == 0)
            {
                logger.LogDebug("Transactions read: {Read}/{Expected}", count, context.TransactionsCount);
            }
        }

        logger.LogDebug("Block parsed; {Read}/{Expected}", count, context.TransactionsCount);

        if (count != context.TransactionsCount)
        {
            logger.LogWarning(
                "Block {Height}, Transaction count doesn't match: {CountInHeader}/{Read}",
                context.Height, context.TransactionsCount, count
            );
        }

        return count;
    }
}
