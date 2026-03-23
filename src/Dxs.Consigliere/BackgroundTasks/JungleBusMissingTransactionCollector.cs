using System.Diagnostics;

using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Microsoft.Extensions.Logging;

using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks;

public class JungleBusMissingTransactionCollector(
    IDocumentStore documentStore,
    ILogger<JungleBusMissingTransactionCollector> logger
) : IJungleBusMissingTransactionCollector
{
    private readonly ILogger _logger = logger;

    public async Task CollectAsync(CancellationToken cancellationToken)
    {
        using var session = documentStore.GetSession();

        var query = session
            .Query<MetaTransaction>()
            .Where(x => x.MissingTransactions.Count > 0)
            .OrderBy(x => x.Height);

        var stream = await session.Advanced
            .StreamAsync(query, out var stats, cancellationToken);

        if (stats.TotalResults == 0)
            return;

        _logger.LogDebug("Found {Count} transactions with missing data", stats.TotalResults);

        var batchSize = 1000;
        var batch = new List<MetaTransaction>();

        while (await stream.MoveNextAsync())
        {
            batch.Add(stream.Current.Document);

            if (batch.Count >= batchSize)
            {
                await StoreMissingTransactions(batch);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await StoreMissingTransactions(batch);
        }
    }

    private async Task StoreMissingTransactions(List<MetaTransaction> metaTransactions)
    {
        var sw = Stopwatch.StartNew();

        using var session = documentStore.GetSession();

        var missingIds = metaTransactions.SelectMany(x => x.MissingTransactions).Distinct();
        var transactions = await session.LoadAsync<MetaTransaction>(missingIds);

        await Parallel.ForEachAsync(
            transactions,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 10
            },
            async (entry, _) =>
            {
                if (entry.Value == null)
                {
                    await documentStore.AddOrUpdateEntity(new MissingTransaction { TxId = entry.Key });
                }
            }
        );

        sw.Stop();

        _logger.LogDebug(
            "{@ProcessedMissingTransactions}",
            new
            {
                transactions.Count,
                ProcessTime = sw.Elapsed,
            }
        );
    }
}
