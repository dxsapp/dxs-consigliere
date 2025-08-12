using System.Collections.Generic;
using Dxs.Bsv.Models;
using Dxs.Common.Extensions;

namespace Dxs.Bsv.Extensions;

public static class TransactionExtensions
{
    public static ulong GetSatoshiAmount(this Transaction tx)
    {
        var sum = tx
            .Outputs
            .AllNotLast()
            .Sum(q => q.Satoshis);
        return sum;
    }

    public static string GetInputId(this Input i) => $"{i.TxId}:{i.Vout}";

    public static IEnumerable<TEntity> AllNotLast<TEntity>(this IList<TEntity> entities)
    {
        for (var i = 0; i < entities.Count - 1; i++)
        {
            yield return entities[i];
        }
    }
}