using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Dataflow;
using Dxs.Common.Interfaces;
using Dxs.Consigliere.Configs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public sealed class BlockObservationJournalMirrorBackgroundTask(
    IBlockMessageBus blockMessageBus,
    BlockObservationJournalWriter journalWriter,
    IOptions<AppConfig> appConfig,
    ILogger<BlockObservationJournalMirrorBackgroundTask> logger
) : IHostedService, IBackgroundTask, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private IDisposable _blockSubscription;
    private IAgent<BlockMessage> _messageHandler;

    public string Name => nameof(BlockObservationJournalMirrorBackgroundTask);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messageHandler ??= Agent.Start<BlockMessage>(HandleAsync, _cts);
        _blockSubscription ??= blockMessageBus.Subscribe(
            message => _messageHandler.Post(message),
            exception => logger.LogError(exception, "Failed to mirror block message to observation journal")
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _blockSubscription?.Dispose();
        _messageHandler?.Complete();
        _cts.Cancel();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _blockSubscription?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task HandleAsync(BlockMessage message)
    {
        try
        {
            if (VNextCutoverMode.IsJournalFirst(appConfig.Value.VNextRuntime.CutoverMode))
                return;

            await journalWriter.AppendConnectedAsync(message, _cts.Token);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to mirror block message {BlockHash} from {Source}", message.BlockHash, message.Source);
        }
    }
}
