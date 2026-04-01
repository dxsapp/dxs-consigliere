using System.Collections.Concurrent;
using System.Diagnostics;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Common.Extensions;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
using Microsoft.Extensions.Logging;

using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks;

public class JungleBusMissingTransactionFetcher(
    IDocumentStore documentStore,
    ITransactionStore transactionStore,
    IRawTransactionFetchService rawTransactionFetchService,
    INetworkProvider networkProvider,
    ILogger<JungleBusMissingTransactionFetcher> logger
) : IJungleBusMissingTransactionFetcher
{
    private readonly ILogger _logger = logger;

    public async Task FetchAsync(CancellationToken cancellationToken)
    {
        using var session = documentStore.GetSession();

        var requests = await session
            .Query<MissingTransaction>()
            .Take(10000)
            .ToListAsync(token: cancellationToken);

        _logger.LogDebug("Found {Count} missing transactions", requests.Count);

        if (requests.Count == 0)
            return;

        const int batchSize = 1000;

        for (var i = 0; i < requests.Count; i += batchSize)
        {
            var sw = Stopwatch.StartNew();
            var bag = new ConcurrentBag<string>();
            var batch = requests.Skip(i).Take(batchSize).ToList();

            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 10
                },
                async (request, _) =>
                {
                    if (await ProcessTransaction(request.TxId))
                    {
                        bag.Add(request.Id);
                    }
                }
            );

            using var session2 = documentStore.GetSession();

            foreach (var requestId in bag)
                session2.Delete(requestId);

            await session2.SaveChangesAsync(cancellationToken);

            sw.Stop();

            _logger.LogDebug(
                "{@FetchMissingTransactions}",
                new
                {
                    BatchSize = batchSize,
                    ProcessTime = sw.Elapsed.TotalSeconds
                }
            );
        }
    }

    private async Task<bool> ProcessTransaction(string transactionId)
    {
        try
        {
            var transactionResult = await rawTransactionFetchService.TryGetAsync(transactionId);
            if (transactionResult?.Raw is not { Length: > 0 } txRaw)
                return false;

            var transaction = Transaction.Parse(txRaw, networkProvider.Network);

            await transactionStore.SaveTransaction(
                transaction,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                null,
                null,
                null,
                null
            );

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to process transaction: {TxId}", transactionId);

            return false;
        }
    }
}
