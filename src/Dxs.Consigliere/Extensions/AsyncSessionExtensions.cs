using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Extensions;

public static class AsyncSessionExtensions
{
    /// <summary>
    /// First item is the total length of a query result
    /// </summary>
    public static async IAsyncEnumerable<(T entity, int totalCount)> Enumerate<T>(
        this IAsyncDocumentSession session,
        IQueryable<T> query
    )
    {
        await using var stream = await session
            .Advanced
            .StreamAsync(query, out var statistics);

        while (await stream.MoveNextAsync())
        {
            yield return (stream.Current.Document, statistics.TotalResults);
        }
    }

    public static async Task<ICollection<T>> Enumerate<T>(
        this IAsyncDocumentSession session,
        IRavenQueryable<T> query
    )
    {
        const int batchSize = 500;

        var batchQuery = query
            .Statistics(out var statistics)
            .Skip(0)
            .Take(batchSize);

        if (statistics.TotalResults > 20000)
            throw new Exception("Too many utxo on a single address");


        var firstBatch = await batchQuery.ToListAsync();
        var result = new List<T>((int)statistics.TotalResults);

        result.AddRange(firstBatch);

        if (statistics.TotalResults > firstBatch.Count)
        {
            var reminder = statistics.TotalResults - firstBatch.Count;
            var batchesLeft = reminder / batchSize;

            for (var i = 0; i < batchesLeft; i++)
            {
                var batch = await query
                    .Skip(result.Count)
                    .Take(batchSize)
                    .ToListAsync();

                result.AddRange(batch);
            }
        }

        return result;
    }
}
