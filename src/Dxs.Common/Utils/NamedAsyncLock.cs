using Nito.AsyncEx;

namespace Dxs.Common.Utils;

/// <summary>
/// <see cref="AsyncLock"/> analogue that can lock on custom key.
/// </summary>
// Based on https://stackoverflow.com/a/31194647/3542151
public class NamedAsyncLock
{
    private class RefCount(AsyncLock value)
    {
        public int Count { get; private set; } = 1;
        public AsyncLock Value { get; } = value ?? throw new ArgumentNullException(nameof(value));

        public void AddRef() => Count++;
        public void RemoveRef() => Count--;
    }

    private class NamedAsyncLockScope(NamedAsyncLock namedLock, string key, IDisposable lockScope): IDisposable
    {
        public void Dispose()
        {
            namedLock.ReleaseLock(key);
            lockScope.Dispose();
        }
    }

    public async Task<IDisposable> LockAsync(string key, CancellationToken cancellationToken)
    {
        var @lock = GetOrCreateLock(key);
        return new NamedAsyncLockScope(this, key, await @lock.LockAsync(cancellationToken));
    }

    public async Task<IDisposable> LockAsync(string key, TimeSpan timeout)
    {
        using var cancellationSource = new CancellationTokenSource(timeout);
        return await LockAsync(key, cancellationSource.Token);
    }

    public Task<IDisposable> LockAsync(string key) => LockAsync(key, CancellationToken.None);

    private readonly Dictionary<string, RefCount> _locks = new ();

    private AsyncLock GetOrCreateLock(string key)
    {
        RefCount refCount;
        lock (_locks)
        {
            if (_locks.TryGetValue(key, out refCount))
            {
                refCount.AddRef();
            }
            else
            {
                refCount = new RefCount(new AsyncLock());
                _locks.Add(key, refCount);
            }
        }

        return refCount.Value;
    }

    private bool ReleaseLock(string key)
    {
        lock (_locks)
        {
            var refCount = _locks[key];
            refCount.RemoveRef();

            if (refCount.Count == 0)
            {
                _locks.Remove(key);
                return true;
            }

            return false;
        }
    }

    public int GetLocksCount()
    {
        lock (_locks) { return _locks.Count; }
    }
}