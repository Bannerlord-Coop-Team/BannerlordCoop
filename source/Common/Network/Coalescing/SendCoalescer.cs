using System;
using System.Collections.Generic;

namespace Common.Network.Coalescing;

/// <inheritdoc cref="ISendCoalescer"/>
public sealed class SendCoalescer : ISendCoalescer
{
    private readonly Dictionary<CoalesceKey, ICoalescedPayload> pending = new();
    private readonly Dictionary<CoalesceKey, CoalescedSendPriority> priorities = new();
    private readonly SortedDictionary<CoalescedSendPriority, List<CoalesceKey>> orderByPriority = new();
    private readonly object gate = new();

    public bool HasPending
    {
        get
        {
            lock (gate)
            {
                return pending.Count > 0;
            }
        }
    }

    public void Enqueue(
        CoalesceKey key,
        ICoalescedPayload payload,
        CoalescedSendPriority priority = CoalescedSendPriority.Normal)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));

        lock (gate)
        {
            if (pending.TryGetValue(key, out var existing))
            {
                if (priorities[key] != priority)
                {
                    throw new InvalidOperationException($"The coalesced key '{key}' was enqueued with different priorities.");
                }

                pending[key] = existing.Merge(payload);
                return;
            }

            pending.Add(key, payload);
            priorities.Add(key, priority);
            if (!orderByPriority.TryGetValue(priority, out var order))
            {
                order = new List<CoalesceKey>();
                orderByPriority.Add(priority, order);
            }

            order.Add(key);
        }
    }

    public void Flush(INetwork network)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));

        ICoalescedPayload[] toSend;
        lock (gate)
        {
            if (pending.Count == 0) return;

            toSend = new ICoalescedPayload[pending.Count];
            int index = 0;
            foreach (var priorityGroup in orderByPriority)
            {
                foreach (var key in priorityGroup.Value)
                {
                    toSend[index++] = pending[key];
                }
            }

            pending.Clear();
            priorities.Clear();
            orderByPriority.Clear();
        }

        foreach (var payload in toSend)
        {
            network.SendAll(payload.ToMessage());
        }
    }

    public void FlushInstance(string instanceId, INetwork network)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));

        List<ICoalescedPayload> toSend = ExtractInstance(instanceId);
        if (toSend == null) return;

        foreach (var payload in toSend)
        {
            network.SendAll(payload.ToMessage());
        }
    }

    public void DropInstance(string instanceId)
    {
        ExtractInstance(instanceId);
    }

    // Removes and returns every pending payload for the instance, or null if none. The caller decides
    // whether to send them (FlushInstance) or discard them (DropInstance).
    private List<ICoalescedPayload> ExtractInstance(string instanceId)
    {
        lock (gate)
        {
            List<ICoalescedPayload> payloads = null;
            List<CoalescedSendPriority> emptyPriorities = null;
            foreach (var priorityGroup in orderByPriority)
            {
                var order = priorityGroup.Value;
                for (int i = 0; i < order.Count;)
                {
                    var key = order[i];
                    if (string.Equals(key.InstanceId, instanceId, StringComparison.Ordinal))
                    {
                        (payloads ??= new List<ICoalescedPayload>()).Add(pending[key]);
                        pending.Remove(key);
                        priorities.Remove(key);
                        order.RemoveAt(i);
                        continue;
                    }

                    i++;
                }

                if (order.Count == 0)
                {
                    (emptyPriorities ??= new List<CoalescedSendPriority>()).Add(priorityGroup.Key);
                }
            }

            if (emptyPriorities != null)
            {
                foreach (var priority in emptyPriorities)
                {
                    orderByPriority.Remove(priority);
                }
            }

            return payloads;
        }
    }
}
