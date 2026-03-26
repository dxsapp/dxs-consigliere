using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;

using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Raven.Client.Documents;
using Raven.Client.Documents.Changes;

namespace Dxs.Consigliere.BackgroundTasks;

public class StasAttributesChangeObserverTask(
    IDocumentStore store,
    IMetaTransactionStore transactionStore,
    IOptions<AppConfig> appConfig,
    ILogger<StasAttributesChangeObserverTask> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    private static readonly TimeSpan LogPeriod = TimeSpan.FromMinutes(1);

    private readonly ILogger _logger = logger;
    private readonly StasDependencyRevalidationCoordinator _coordinator = new(store, transactionStore, logger);

    private int _processedChanges;

    protected override TimeSpan Period => TimeSpan.FromSeconds(1);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(5);

    public override string Name => nameof(StasAttributesChangeObserverTask);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var actionBlock = new ActionBlock<(string Id, DocumentChangeTypes Type)>(
            HandleBatch,
            new()
            {
                MaxDegreeOfParallelism = 1
            });

        using var sub = store
            .Changes()
            .ForDocumentsInCollection("MetaTransactions")
            .Where(x => x.Type is DocumentChangeTypes.Put or DocumentChangeTypes.Delete)
            .Select(x => (x.Id, x.Type))
            .Subscribe(x => actionBlock.Post(x));

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(LogPeriod, cancellationToken);

            var processedForLogPeriod = Interlocked.Exchange(ref _processedChanges, 0);

            _logger.LogInformation("{@DocumentChangeHandlingStats}", new
            {
                HandledChanges = processedForLogPeriod,
                QueueSize = actionBlock.InputCount * 250
            });
        }
    }

    private async Task HandleBatch((string Id, DocumentChangeTypes Type) change)
    {
        _processedChanges += 1;

        try
        {
            if (change.Type == DocumentChangeTypes.Delete)
                await _coordinator.HandleTransactionDeletedAsync(change.Id);
            else
                await _coordinator.HandleTransactionChangedAsync(change.Id);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to handle MetaTransaction change for {TxId}", change.Id);
        }
    }
}
