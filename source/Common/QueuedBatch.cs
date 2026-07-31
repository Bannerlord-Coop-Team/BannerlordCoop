using System;
using System.Collections.Generic;
using System.Threading;

namespace Common;

/// <summary>Items owned by one adjacent game-thread queue segment.</summary>
internal sealed class QueuedBatch<T>
{
    private readonly Action<T> action;
    private readonly CancellationToken cancellation;
    private readonly CancellationToken lifetime;
    private readonly string context;
    private readonly object itemsLock = new object();
    private readonly List<T> items = new List<T>();
    private readonly CancellationTokenRegistration lifetimeRegistration;

    public QueuedBatch(
        Action<T> action,
        CancellationToken cancellation,
        CancellationToken lifetime,
        string context,
        T firstItem)
    {
        this.action = action;
        this.cancellation = cancellation;
        this.lifetime = lifetime;
        this.context = context;
        items.Add(firstItem);
        lifetimeRegistration = lifetime.CanBeCanceled
            ? lifetime.Register(Clear)
            : default;
    }

    public bool TryAdd(
        Action<T> candidateAction,
        CancellationToken candidateCancellation,
        CancellationToken candidateLifetime,
        T item)
    {
        if (!ReferenceEquals(action, candidateAction) ||
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
                items.Add(item);
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
                if (cancellation.IsCancellationRequested ||
                    lifetime.IsCancellationRequested)
                {
                    items.Clear();
                    return;
                }

                snapshot = items.ToArray();
                items.Clear();
            }

            foreach (T item in snapshot)
            {
                if (cancellation.IsCancellationRequested ||
                    lifetime.IsCancellationRequested) return;
                GameThread.InvokeSafe(action, item, context);
            }
        }
        finally
        {
            lifetimeRegistration.Dispose();
        }
    }

    private void Clear()
    {
        lock (itemsLock)
        {
            items.Clear();
        }
    }
}
