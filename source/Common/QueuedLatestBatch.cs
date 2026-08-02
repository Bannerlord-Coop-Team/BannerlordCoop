using System;
using System.Collections.Generic;
using System.Threading;

namespace Common;

/// <summary>Latest continuous value per key within one adjacent game-thread queue segment.</summary>
internal sealed class QueuedLatestBatch<TKey, T>
{
    private readonly Func<T, TKey> keySelector;
    private readonly Action<IReadOnlyList<T>> action;
    private readonly CancellationToken cancellation;
    private readonly CancellationToken lifetime;
    private readonly string context;
    private readonly object itemsLock = new object();
    private readonly List<TKey> order = new List<TKey>();
    private readonly Dictionary<TKey, T> latest = new Dictionary<TKey, T>();
    private readonly CancellationTokenRegistration lifetimeRegistration;

    public QueuedLatestBatch(
        Func<T, TKey> keySelector,
        Action<IReadOnlyList<T>> action,
        CancellationToken cancellation,
        CancellationToken lifetime,
        string context,
        IEnumerable<T> firstItems)
    {
        this.keySelector = keySelector;
        this.action = action;
        this.cancellation = cancellation;
        this.lifetime = lifetime;
        this.context = context;
        Add(firstItems);
        lifetimeRegistration = lifetime.CanBeCanceled
            ? lifetime.Register(Clear)
            : default;
    }

    public bool TryAdd(
        Func<T, TKey> candidateKeySelector,
        Action<IReadOnlyList<T>> candidateAction,
        CancellationToken candidateCancellation,
        CancellationToken candidateLifetime,
        IEnumerable<T> items)
    {
        if (!ReferenceEquals(keySelector, candidateKeySelector) ||
            !ReferenceEquals(action, candidateAction) ||
            cancellation != candidateCancellation ||
            lifetime != candidateLifetime)
        {
            return false;
        }

        lock (itemsLock)
        {
            if (!cancellation.IsCancellationRequested &&
                !lifetime.IsCancellationRequested)
            {
                Add(items);
            }
        }
        return true;
    }

    public void Run()
    {
        try
        {
            T[] snapshot;
            lock (itemsLock)
            {
                if (cancellation.IsCancellationRequested || lifetime.IsCancellationRequested)
                {
                    ClearLocked();
                    return;
                }

                snapshot = new T[order.Count];
                for (int i = 0; i < order.Count; i++)
                    snapshot[i] = latest[order[i]];
                ClearLocked();
            }

            if (cancellation.IsCancellationRequested || lifetime.IsCancellationRequested) return;
            GameThread.InvokeSafe(() => action(snapshot), context);
        }
        finally
        {
            lifetimeRegistration.Dispose();
        }
    }

    private void Add(IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            TKey key = keySelector(item);
            if (!latest.ContainsKey(key)) order.Add(key);
            latest[key] = item;
        }
    }

    private void Clear()
    {
        lock (itemsLock)
        {
            ClearLocked();
        }
    }

    private void ClearLocked()
    {
        order.Clear();
        latest.Clear();
    }
}
