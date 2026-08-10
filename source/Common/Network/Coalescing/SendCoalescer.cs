using System;
using System.Collections.Generic;

namespace Common.Network.Coalescing;

/// <inheritdoc cref="ISendCoalescer"/>
public sealed class SendCoalescer : ISendCoalescer
{
    private readonly Dictionary<CoalesceKey, ICoalescedPayload> pending = new();
    private readonly List<CoalesceKey> order = new();
    private readonly object gate = new();

#if DEBUG
    private CoalesceKey? debugTraceKey;
    private int debugTraceEnqueued;
    private int debugTraceMerged;
    private int debugTraceSent;
#endif

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
            if (pending.TryGetValue(key, out var existing))
            {
#if DEBUG
                RecordDebugEnqueue(key, merged: true);
#endif
                pending[key] = existing.Merge(payload);
                return;
            }

#if DEBUG
            RecordDebugEnqueue(key, merged: false);
#endif
            pending.Add(key, payload);
            order.Add(key);
        }
    }

    public void Flush(INetwork network)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));

        ICoalescedPayload[] toSend;
#if DEBUG
        CoalesceKey[] debugKeys;
#endif
        lock (gate)
        {
            if (pending.Count == 0) return;

            toSend = new ICoalescedPayload[pending.Count];
#if DEBUG
            debugKeys = new CoalesceKey[pending.Count];
#endif
            for (int i = 0; i < order.Count; i++)
            {
                toSend[i] = pending[order[i]];
#if DEBUG
                debugKeys[i] = order[i];
#endif
            }

            pending.Clear();
            order.Clear();
        }

        for (int i = 0; i < toSend.Length; i++)
        {
            network.SendAll(toSend[i].ToMessage());
#if DEBUG
            lock (gate)
            {
                RecordDebugSend(debugKeys[i]);
            }
#endif
        }
    }

    public void FlushInstance(string instanceId, INetwork network)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));

        List<PendingEntry> toSend = ExtractInstance(instanceId);
        if (toSend == null) return;

        foreach (var entry in toSend)
        {
            network.SendAll(entry.Payload.ToMessage());
#if DEBUG
            lock (gate)
            {
                RecordDebugSend(entry.Key);
            }
#endif
        }
    }

    public void DropInstance(string instanceId)
    {
        ExtractInstance(instanceId);
    }

    // Removes and returns every pending payload for the instance, or null if none. The caller decides
    // whether to send them (FlushInstance) or discard them (DropInstance).
    private List<PendingEntry> ExtractInstance(string instanceId)
    {
        lock (gate)
        {
            List<PendingEntry> payloads = null;
            for (int i = 0; i < order.Count;)
            {
                var key = order[i];
                if (string.Equals(key.InstanceId, instanceId, StringComparison.Ordinal))
                {
                    (payloads ??= new List<PendingEntry>()).Add(
                        new PendingEntry(key, pending[key]));
                    pending.Remove(key);
                    order.RemoveAt(i);
                    continue;
                }

                i++;
            }

            return payloads;
        }
    }

    private readonly struct PendingEntry
    {
        public CoalesceKey Key { get; }
        public ICoalescedPayload Payload { get; }

        public PendingEntry(CoalesceKey key, ICoalescedPayload payload)
        {
            Key = key;
            Payload = payload;
        }
    }

#if DEBUG
    public bool TryStartDebugTrace(CoalesceKey key)
    {
        lock (gate)
        {
            if (debugTraceKey.HasValue || pending.ContainsKey(key))
            {
                return false;
            }

            debugTraceKey = key;
            debugTraceEnqueued = 0;
            debugTraceMerged = 0;
            debugTraceSent = 0;
            return true;
        }
    }

    public DebugTraceSnapshot GetDebugTraceSnapshot()
    {
        lock (gate)
        {
            if (!debugTraceKey.HasValue)
            {
                throw new InvalidOperationException("No send-coalescer debug trace is active.");
            }

            return new DebugTraceSnapshot(
                debugTraceKey.Value,
                debugTraceEnqueued,
                debugTraceMerged,
                debugTraceSent,
                pending.ContainsKey(debugTraceKey.Value));
        }
    }

    public void StopDebugTrace()
    {
        lock (gate)
        {
            debugTraceKey = null;
            debugTraceEnqueued = 0;
            debugTraceMerged = 0;
            debugTraceSent = 0;
        }
    }

    private void RecordDebugEnqueue(CoalesceKey key, bool merged)
    {
        if (!debugTraceKey.HasValue || !debugTraceKey.Value.Equals(key))
        {
            return;
        }

        debugTraceEnqueued++;
        if (merged)
        {
            debugTraceMerged++;
        }
    }

    private void RecordDebugSend(CoalesceKey key)
    {
        if (debugTraceKey.HasValue && debugTraceKey.Value.Equals(key))
        {
            debugTraceSent++;
        }
    }

    public sealed class DebugTraceSnapshot
    {
        public CoalesceKey Key { get; }
        public int Enqueued { get; }
        public int Merged { get; }
        public int Sent { get; }
        public bool Pending { get; }

        public DebugTraceSnapshot(
            CoalesceKey key,
            int enqueued,
            int merged,
            int sent,
            bool pending)
        {
            Key = key;
            Enqueued = enqueued;
            Merged = merged;
            Sent = sent;
            Pending = pending;
        }
    }
#endif
}
