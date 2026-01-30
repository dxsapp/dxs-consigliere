using System.Collections;
using System.Runtime.CompilerServices;

using TrustMargin.Common.Extensions;

namespace Dxs.Common.Extensions;

public static class AsyncEnumerableEx
{
    public class Batch<T> : IList<T>
    {
        private readonly IList<T> _list;

        public int Skip { get; }
        public int Take { get; }

        public bool IsFirst => Skip == 0;

        public Batch(int skip, int take, IList<T> list)
        {
            _list = list;

            Skip = skip;
            Take = take;
        }

        #region Implementation of IEnumerable

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_list).GetEnumerator();
        }

        #endregion

        #region Implementation of ICollection<T>

        public void Add(T item)
        {
            _list.Add(item);
        }

        public void Clear()
        {
            _list.Clear();
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            return _list.Remove(item);
        }

        public int Count => _list.Count;

        public bool IsReadOnly => _list.IsReadOnly;

        #endregion

        #region Implementation of IList<T>

        public int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            _list.RemoveAt(index);
        }

        public T this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }

        #endregion

        public override string ToString() => $"{Skip}..{Skip + Take}[{Count}]";
    }

    private static async IAsyncEnumerable<Batch<T>> BatchAsync<T>(
        Func<(int skip, int take, CancellationToken cancellationToken), Task<IEnumerable<T>>> batchProvider,
        int batchSize,
        bool cycle,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            throw new ArgumentException($"{nameof(batchSize)} must be positive.");

        var from = 0;
        while (true)
        {
            var batch = (await batchProvider((skip: from, take: batchSize, cancellationToken)))
                .AsIList();

            if (from == 0 && batch.Count == 0)
                break; // empty collection

            if (EnumerableExtensions.Any(batch))
                yield return new Batch<T>(from, batchSize, batch);

            if (batch.Count < batchSize)
            {
                if (cycle)
                {
                    from = 0;
                    continue;
                }

                break;
            }

            from += batch.Count;
        }
    }

    public static IAsyncEnumerable<Batch<T>> BatchAsync<T>(
        Func<(int skip, int take, CancellationToken cancellationToken), Task<IEnumerable<T>>> batchProvider,
        int batchSize,
        CancellationToken cancellationToken = default
    ) =>
        BatchAsync(batchProvider, batchSize, cycle: false, cancellationToken);

    public static IAsyncEnumerable<Batch<T>> CycleBatchAsync<T>(
        Func<(int skip, int take, CancellationToken cancellationToken), Task<IEnumerable<T>>> batchProvider,
        int batchSize,
        CancellationToken cancellationToken = default
    ) =>
        BatchAsync(batchProvider, batchSize, cycle: true, cancellationToken);

    public static IAsyncEnumerable<Batch<T>> BatchAsync<T>(
        Func<(int skip, int take, CancellationToken cancellationToken), Task<IList<T>>> batchProvider,
        int batchSize,
        CancellationToken cancellationToken = default
    ) =>
        BatchAsync<T>(async arg => await batchProvider(arg), batchSize, cycle: false, cancellationToken);

    public static IAsyncEnumerable<Batch<T>> CycleBatchAsync<T>(
        Func<(int skip, int take, CancellationToken cancellationToken), Task<IList<T>>> batchProvider,
        int batchSize,
        CancellationToken cancellationToken = default
    ) =>
        BatchAsync<T>(async arg => await batchProvider(arg), batchSize, cycle: true, cancellationToken);

    public static IAsyncEnumerable<Batch<T>> BatchAsync<T>(
        Func<(int skip, int take, CancellationToken cancellationToken), Task<T[]>> batchProvider,
        int batchSize,
        CancellationToken cancellationToken = default
    ) =>
        BatchAsync<T>(async arg => await batchProvider(arg), batchSize, cycle: false, cancellationToken);

    public static IAsyncEnumerable<Batch<T>> CycleBatchAsync<T>(
        Func<(int skip, int take, CancellationToken cancellationToken), Task<T[]>> batchProvider,
        int batchSize,
        CancellationToken cancellationToken = default
    ) =>
        BatchAsync<T>(async arg => await batchProvider(arg), batchSize, cycle: true, cancellationToken);
}
