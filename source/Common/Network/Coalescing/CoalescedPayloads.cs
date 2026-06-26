using Common.Messaging;
using System;

namespace Common.Network.Coalescing;

/// <summary>
/// Keeps the most recent value; older pending values for the key are discarded. Use for absolute-value
/// sets, where each update carries the new value of a field and only the latest one matters.
/// </summary>
public sealed class LatestWinsPayload : ICoalescedPayload
{
    private readonly IMessage message;

    public LatestWinsPayload(IMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        this.message = message;
    }

    public ICoalescedPayload Merge(ICoalescedPayload incoming)
    {
        if (incoming is not LatestWinsPayload)
        {
            throw new ArgumentException(
                $"Cannot merge {incoming?.GetType().Name ?? "null"} into LatestWinsPayload; " +
                "a coalesce key must use a single payload type.",
                nameof(incoming));
        }

        return incoming;
    }

    public IMessage ToMessage() => message;
}

/// <summary>
/// Keeps the most recent message producer and invokes it at flush time, so a large message is built
/// once from the latest live state instead of on every change. Use for whole-object / whole-roster
/// snapshots.
/// </summary>
public sealed class SnapshotPayload : ICoalescedPayload
{
    private readonly Func<IMessage> producer;

    public SnapshotPayload(Func<IMessage> producer)
    {
        if (producer == null) throw new ArgumentNullException(nameof(producer));
        this.producer = producer;
    }

    public ICoalescedPayload Merge(ICoalescedPayload incoming)
    {
        if (incoming is not SnapshotPayload)
        {
            throw new ArgumentException(
                $"Cannot merge {incoming?.GetType().Name ?? "null"} into SnapshotPayload; " +
                "a coalesce key must use a single payload type.",
                nameof(incoming));
        }

        return incoming;
    }

    public IMessage ToMessage() => producer();
}

/// <summary>
/// Accumulates additive deltas. Use for summed quantities (xp gained, gold change) where the merged
/// value is the running total of every delta enqueued for the key this tick.
/// </summary>
/// <typeparam name="T">The delta value type, for example <see cref="int"/>.</typeparam>
public sealed class SummedPayload<T> : ICoalescedPayload
{
    private readonly T value;
    private readonly Func<T, T, T> add;
    private readonly Func<T, IMessage> build;

    public SummedPayload(T value, Func<T, T, T> add, Func<T, IMessage> build)
    {
        if (add == null) throw new ArgumentNullException(nameof(add));
        if (build == null) throw new ArgumentNullException(nameof(build));
        this.value = value;
        this.add = add;
        this.build = build;
    }

    public ICoalescedPayload Merge(ICoalescedPayload incoming)
    {
        if (incoming is not SummedPayload<T> other)
        {
            throw new ArgumentException(
                $"Cannot merge {incoming?.GetType().Name ?? "null"} into SummedPayload<{typeof(T).Name}>; " +
                "a coalesce key must use a single payload type.",
                nameof(incoming));
        }

        return new SummedPayload<T>(add(value, other.value), add, build);
    }

    public IMessage ToMessage() => build(value);
}
