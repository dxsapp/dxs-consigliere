using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dxs.Bsv.Models;
using Dxs.Common.Exceptions.Transactions;
using Dxs.Common.Extensions;
using Dxs.Common.Interfaces;
using Dxs.Common.Utils;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Bsv.Factories.Impl;

public class UtxoCache(
    IUtxoSetProvider utxoProvider,
    IAppCache<UtxoCache> utxoCache,
    IOptions<UtxoCache.CacheDuration> cacheDurationOptions,
    ILogger<UtxoCache> logger
) : IUtxoCache
{
    public class CacheDuration
    {
        public TimeSpan Enumerated { get; set; }
        public TimeSpan Broadcasted { get; set; }
    }

    private readonly CacheDuration _cacheDuration = cacheDurationOptions.Value;

    private readonly Dictionary<string, List<OutPoint>> _byAddressAndToken = new();
    private readonly ConcurrentDictionary<string, IAsyncEnumerator<OutPoint>> _enumerators = new();
    private readonly NamedAsyncLock _asyncLock = new();

    public bool IsUsed(OutPoint outPoint, out bool broadcasted) => utxoCache.TryGet(ToKey(outPoint), out broadcasted);

    public void MarkUsed(OutPoint outPoint, bool broadcasted) => MarkUsed(outPoint, ToKey(outPoint), broadcasted);

    private void MarkUsed(OutPoint utxo, string key, bool broadcasted)
    {
        var status = broadcasted ? "broadcasted" : "enumerated";
        var duration = broadcasted ? _cacheDuration.Broadcasted : _cacheDuration.Enumerated;

        if (duration == TimeSpan.Zero)
            return;

        if (broadcasted)
            utxoCache.Set(key, true, relativeExpiration: duration);
        else
            utxoCache.GetOrAdd(key, false, relativeExpiration: duration);

        logger.LogDebug("(Re)Marked {@UtxoOutput} as {UtxoStatus} for {UtxoCacheDuration}, {UtxoCachedCount} UTXOs in cache",
            utxo, status, duration, utxoCache.Count
        );
    }

    /// Returns minimal UTXO set for requested satoshis amount, to minimize merges count
    public async Task<List<OutPoint>> GetStasUtxos(ulong requestedSatoshis, Address address, TokenId tokenId)
    {
        var key = ToKey(address, tokenId);
        using var _ = await _asyncLock.LockAsync(key, TimeSpan.FromSeconds(5));

        var balance = 0Ul;

        if (!_byAddressAndToken.TryGetValue(key, out var outPoints))
            (outPoints, balance) = await LoadUtxoSet(address, tokenId);
        else if (balance < requestedSatoshis)
            (outPoints, balance) = await LoadUtxoSet(address, tokenId);

        if (balance < requestedSatoshis)
            throw new NotEnoughMoneyException(
                new HashSet<string> { address.Value },
                balance,
                string.Empty,
                requestedSatoshis,
                0,
                tokenId?.Value
            );

        return GetExactAmount(outPoints, requestedSatoshis);
    }

    public async Task<OutPoint?> GetNextUtxoOrNull(Address address)
    {
        var key = ToKey(address);
        var enumerator = _enumerators.GetOrAdd(
            key,
            _ => EnumerateOutPoints(address).GetAsyncEnumerator()
        );

        bool nextFetched;
        try
        {
            nextFetched = await enumerator.MoveNextAsync();
        }
        catch (Exception exception)
        {
            _enumerators.TryRemove(key, enumerator); // restart enumeration
            throw exception.Dispatch();
        }

        if (!nextFetched)
        {
            _enumerators.TryRemove(key, enumerator); // restart enumeration
        }

        return nextFetched ? enumerator.Current : null;
    }

    private List<OutPoint> GetExactAmount(IList<OutPoint> source, ulong requestedSatoshis)
    {
        var sorted = source.OrderBy(x => x.Satoshis).ToList();
        var exactOrGreater = sorted.FirstOrDefault(x => x.Satoshis >= requestedSatoshis);

        if (!exactOrGreater.IsDefault && exactOrGreater.Satoshis == requestedSatoshis)
        {
            source.Remove(exactOrGreater);
            return new List<OutPoint> { exactOrGreater };
        }

        var result = new List<OutPoint>();
        var accumulated = 0UL;

        using var less = sorted
            .Where(x => x.Satoshis < requestedSatoshis)
            .OrderByDescending(x => x.Satoshis)
            .GetEnumerator();

        while (less.MoveNext())
        {
            var outPoint = less.Current;

            result.Add(outPoint);
            accumulated += outPoint.Satoshis;

            if (accumulated >= requestedSatoshis)
            {
                foreach (var x in result)
                    source.Remove(x);

                return result;
            }
        }

        source.Remove(exactOrGreater);

        return new List<OutPoint> { exactOrGreater };
    }

    private async Task<(List<OutPoint> utxoSet, ulong Satoshis)> LoadUtxoSet(
        Address address,
        TokenId tokenId = null
    )
    {
        var key = ToKey(address, tokenId);
        var outPoints = await utxoProvider.GetUtxoSet(address, tokenId);

        if (outPoints.Count > 5000)
            logger.LogWarning("UtxoSet is too large to keep it in memory");

        var utxoSet = new List<OutPoint>();
        var satoshis = 0UL;

        foreach (var outPoint in outPoints)
        {
            if (!IsUsed(outPoint, out _))
            {
                utxoSet.Add(outPoint);
                satoshis += outPoint.Satoshis;
            }
        }

        _byAddressAndToken[key] = utxoSet;

        return (utxoSet, satoshis);
    }

    /// <summary>
    /// at the moment we have max 500 utxos, so there is no need to use pagination and batching,
    /// but in the future it could be very useful, so lets keep it
    /// </summary>
    private async IAsyncEnumerable<OutPoint> EnumerateOutPoints(Address address)
    {
        var isCycleSkipped = true;
        var (skippedCyclesCount, skippedCyclesMax) = (0, 1);

        var batches = AsyncEnumerableEx.CycleBatchAsync(
            async _ =>
            {
                var (outPoints, _) = await LoadUtxoSet(address);

                return outPoints.AsIList();
            },
            int.MaxValue
        );

        await foreach (var batch in batches)
        {
            if (batch.IsFirst)
            {
                if (skippedCyclesCount >= skippedCyclesMax)
                {
                    logger.LogWarning("{Name} didn't provide new UTXO in {Count} cycles",
                        utxoProvider.GetType().Name, skippedCyclesCount
                    );

                    yield break;
                }

                isCycleSkipped = true;
                logger.LogDebug("Started to filter new UTXO cycle {$UtxoBatch}", batch);
            }

            foreach (var output in batch)
            {
                if (IsUsed(output, out var broadcasted))
                {
                    var status = broadcasted ? "broadcasted" : "enumerated";
                    logger.LogWarning("Excluded {UtxoStatus} {ExcludedUtxo} duplicate from UTXOs", status, output);

                    continue;
                }

                isCycleSkipped = false;
                MarkUsed(output, broadcasted: false);

                yield return output;
            }

            if (batch.IsFirst && isCycleSkipped)
                skippedCyclesCount++;
        }
    }

    private static string ToKey(Address address, TokenId tokenId = null)
        => tokenId == null
            ? $"utxo-cache:{address}"
            : $"utxo-cache:{address}/{tokenId}";

    private static string ToKey(OutPoint outPoint) => ToKey(outPoint.TransactionId, outPoint.Vout);

    private static string ToKey(string transactionId, uint outputIndex) => $"{transactionId}:{outputIndex}";
}
