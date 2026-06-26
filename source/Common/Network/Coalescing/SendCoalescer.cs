using System;
using System.Collections.Generic;

namespace Common.Network.Coalescing;

/// <inheritdoc cref="ISendCoalescer"/>
public sealed class SendCoalescer : ISendCoalescer
{
    private readonly Dictionary<CoalesceKey, ICoalescedPayload> pending = new();
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

    public void Enqueue(CoalesceKey key, ICoalescedPayload payload)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));

        lock (gate)
        {
            pending[key] = pending.TryGetValue(key, out var existing)
                ? existing.Merge(payload)
                : payload;
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
            pending.Values.CopyTo(toSend, 0);
            pending.Clear();
        }

        // Build and send outside the lock: ToMessage and SendAll do real work and we never hold the
        // buffer lock across network I/O.
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
            List<CoalesceKey> keys = null;
            foreach (var key in pending.Keys)
            {
                if (string.Equals(key.InstanceId, instanceId, StringComparison.Ordinal))
                {
                    (keys ??= new List<CoalesceKey>()).Add(key);
                }
            }

            if (keys == null) return null;

            var payloads = new List<ICoalescedPayload>(keys.Count);
            foreach (var key in keys)
            {
                payloads.Add(pending[key]);
                pending.Remove(key);
            }

            return payloads;
        }
    }
}
