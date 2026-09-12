using System;
using System.Collections.Generic;
using LiteNetLib;

namespace Common.Network.Coalescing;

/// <inheritdoc cref="ISendCoalescer"/>
public sealed class SendCoalescer : ISendCoalescer
{
    private enum DeliveryMode
    {
        Broadcast,
        Peer,
        AllButPeer,
    }

    private sealed class PendingSend
    {
        public ICoalescedPayload Payload { get; set; }
        public DeliveryMode Mode { get; }
        public NetPeer Peer { get; }

        public PendingSend(ICoalescedPayload payload, DeliveryMode mode, NetPeer peer)
        {
            Payload = payload;
            Mode = mode;
            Peer = peer;
        }

        public void Send(INetwork network)
        {
            var message = Payload.ToMessage();
            switch (Mode)
            {
                case DeliveryMode.Broadcast:
                    network.SendAll(message);
                    break;
                case DeliveryMode.Peer:
                    network.Send(Peer, message);
                    break;
                case DeliveryMode.AllButPeer:
                    network.SendAllBut(Peer, message);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown coalesced delivery mode {Mode}.");
            }
        }
    }

    private readonly Dictionary<CoalesceKey, PendingSend> pending = new();
    private readonly List<CoalesceKey> order = new();
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
        Enqueue(key, payload, DeliveryMode.Broadcast, null);
    }

    public void EnqueueToPeer(CoalesceKey key, ICoalescedPayload payload, NetPeer peer)
    {
        if (peer == null) throw new ArgumentNullException(nameof(peer));
        Enqueue(key, payload, DeliveryMode.Peer, peer);
    }

    public void EnqueueToAllBut(CoalesceKey key, ICoalescedPayload payload, NetPeer excludedPeer)
    {
        if (excludedPeer == null) throw new ArgumentNullException(nameof(excludedPeer));
        Enqueue(key, payload, DeliveryMode.AllButPeer, excludedPeer);
    }

    private void Enqueue(CoalesceKey key, ICoalescedPayload payload, DeliveryMode mode, NetPeer peer)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));

        lock (gate)
        {
            if (pending.TryGetValue(key, out var existing))
            {
                if (existing.Mode != mode || !ReferenceEquals(existing.Peer, peer))
                {
                    throw new InvalidOperationException(
                        $"Coalesce key {key} cannot reuse a different delivery route.");
                }

                existing.Payload = existing.Payload.Merge(payload);
                return;
            }

            pending.Add(key, new PendingSend(payload, mode, peer));
            order.Add(key);
        }
    }

    public void Flush(INetwork network)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));

        PendingSend[] toSend;
        lock (gate)
        {
            if (pending.Count == 0) return;

            toSend = new PendingSend[pending.Count];
            for (int i = 0; i < order.Count; i++)
            {
                toSend[i] = pending[order[i]];
            }

            pending.Clear();
            order.Clear();
        }

        foreach (var pendingSend in toSend)
        {
            pendingSend.Send(network);
        }
    }

    public void FlushInstance(string instanceId, INetwork network)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));

        List<PendingSend> toSend = ExtractInstance(instanceId);
        if (toSend == null) return;

        foreach (var pendingSend in toSend)
        {
            pendingSend.Send(network);
        }
    }

    public void DropInstance(string instanceId)
    {
        ExtractInstance(instanceId);
    }

    // Removes and returns every pending payload for the instance, or null if none. The caller decides
    // whether to send them (FlushInstance) or discard them (DropInstance).
    private List<PendingSend> ExtractInstance(string instanceId)
    {
        lock (gate)
        {
            List<PendingSend> payloads = null;
            for (int i = 0; i < order.Count;)
            {
                var key = order[i];
                if (string.Equals(key.InstanceId, instanceId, StringComparison.Ordinal))
                {
                    (payloads ??= new List<PendingSend>()).Add(pending[key]);
                    pending.Remove(key);
                    order.RemoveAt(i);
                    continue;
                }

                i++;
            }

            return payloads;
        }
    }
}
