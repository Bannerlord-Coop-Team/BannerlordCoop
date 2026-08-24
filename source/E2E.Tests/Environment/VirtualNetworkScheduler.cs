using System;
using System.Collections.Generic;

namespace E2E.Tests.Environment;

public interface IVirtualNetworkScheduler
{
    TimeSpan CurrentTime { get; }
    TimeSpan DefaultLatency { get; set; }
    int PendingDeliveryCount { get; }

    void SetLatency(object sender, object receiver, TimeSpan latency);
    void ClearLatency(object sender, object receiver);
    void Schedule(object sender, object receiver, string channel, Action delivery);
    int Cancel(object endpoint);
    int AdvanceBy(TimeSpan elapsed);
    int DrainReady();
    int RunUntilIdle();
}

/// <summary>
/// Deterministic virtual-time queue for in-process network tests. FIFO is retained per directed link and
/// channel even when the configured latency is reduced while older traffic is pending.
/// </summary>
public class VirtualNetworkScheduler : IVirtualNetworkScheduler
{
    private readonly List<ScheduledDelivery> pending = new();
    private readonly Dictionary<EndpointPair, TimeSpan> linkLatencies = new();
    private readonly Dictionary<DeliveryStream, TimeSpan> lastDueByStream = new();
    private TimeSpan defaultLatency;
    private long nextSequence;

    public TimeSpan CurrentTime { get; private set; }

    public TimeSpan DefaultLatency
    {
        get => defaultLatency;
        set
        {
            ValidateLatency(value, nameof(value));
            defaultLatency = value;
        }
    }

    public int PendingDeliveryCount => pending.Count;

    public void SetLatency(object sender, object receiver, TimeSpan latency)
    {
        ValidateEndpoint(sender, nameof(sender));
        ValidateEndpoint(receiver, nameof(receiver));
        ValidateLatency(latency, nameof(latency));
        linkLatencies[new EndpointPair(sender, receiver)] = latency;
    }

    public void ClearLatency(object sender, object receiver)
    {
        ValidateEndpoint(sender, nameof(sender));
        ValidateEndpoint(receiver, nameof(receiver));
        linkLatencies.Remove(new EndpointPair(sender, receiver));
    }

    public void Schedule(object sender, object receiver, string channel, Action delivery)
    {
        ValidateEndpoint(sender, nameof(sender));
        ValidateEndpoint(receiver, nameof(receiver));
        if (string.IsNullOrEmpty(channel)) throw new ArgumentException("A channel is required", nameof(channel));
        if (delivery == null) throw new ArgumentNullException(nameof(delivery));

        var endpoints = new EndpointPair(sender, receiver);
        var stream = new DeliveryStream(endpoints, channel);
        TimeSpan latency = linkLatencies.TryGetValue(endpoints, out TimeSpan configuredLatency)
            ? configuredLatency
            : DefaultLatency;
        TimeSpan due = CurrentTime + latency;

        if (lastDueByStream.TryGetValue(stream, out TimeSpan previousDue) && due < previousDue)
            due = previousDue;

        lastDueByStream[stream] = due;
        pending.Add(new ScheduledDelivery(sender, receiver, due, nextSequence++, delivery));
    }

    public int Cancel(object endpoint)
    {
        ValidateEndpoint(endpoint, nameof(endpoint));

        int removed = pending.RemoveAll(delivery =>
            ReferenceEquals(delivery.Sender, endpoint) ||
            ReferenceEquals(delivery.Receiver, endpoint));

        var affectedStreams = lastDueByStream.Keys
            .Where(stream =>
                ReferenceEquals(stream.Endpoints.Sender, endpoint) ||
                ReferenceEquals(stream.Endpoints.Receiver, endpoint))
            .ToArray();
        foreach (DeliveryStream stream in affectedStreams)
            lastDueByStream.Remove(stream);

        return removed;
    }

    public int AdvanceBy(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));

        CurrentTime += elapsed;
        return DrainReady();
    }

    public int DrainReady()
    {
        int delivered = 0;

        while (TryTakeNext(CurrentTime, out ScheduledDelivery next))
        {
            next.Deliver();
            delivered++;
        }

        return delivered;
    }

    public int RunUntilIdle()
    {
        int delivered = 0;

        while (pending.Count > 0)
        {
            TimeSpan nextDue = pending.Min(delivery => delivery.Due);
            if (nextDue > CurrentTime)
                CurrentTime = nextDue;

            delivered += DrainReady();
        }

        return delivered;
    }

    private bool TryTakeNext(TimeSpan maximumDue, out ScheduledDelivery next)
    {
        int nextIndex = -1;
        next = default;

        for (int i = 0; i < pending.Count; i++)
        {
            ScheduledDelivery candidate = pending[i];
            if (candidate.Due > maximumDue) continue;
            if (nextIndex >= 0 && Compare(candidate, next) >= 0) continue;

            nextIndex = i;
            next = candidate;
        }

        if (nextIndex < 0) return false;

        pending.RemoveAt(nextIndex);
        return true;
    }

    private static int Compare(ScheduledDelivery first, ScheduledDelivery second)
    {
        int dueComparison = first.Due.CompareTo(second.Due);
        return dueComparison != 0
            ? dueComparison
            : first.Sequence.CompareTo(second.Sequence);
    }

    private static void ValidateEndpoint(object endpoint, string parameterName)
    {
        if (endpoint == null) throw new ArgumentNullException(parameterName);
    }

    private static void ValidateLatency(TimeSpan latency, string parameterName)
    {
        if (latency < TimeSpan.Zero) throw new ArgumentOutOfRangeException(parameterName);
    }

    private readonly struct EndpointPair : IEquatable<EndpointPair>
    {
        public object Sender { get; }
        public object Receiver { get; }

        public EndpointPair(object sender, object receiver)
        {
            Sender = sender;
            Receiver = receiver;
        }

        public bool Equals(EndpointPair other) =>
            ReferenceEquals(Sender, other.Sender) &&
            ReferenceEquals(Receiver, other.Receiver);

        public override bool Equals(object? obj) => obj is EndpointPair other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Sender),
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Receiver));
    }

    private readonly struct DeliveryStream : IEquatable<DeliveryStream>
    {
        public EndpointPair Endpoints { get; }
        public string Channel { get; }

        public DeliveryStream(EndpointPair endpoints, string channel)
        {
            Endpoints = endpoints;
            Channel = channel;
        }

        public bool Equals(DeliveryStream other) =>
            Endpoints.Equals(other.Endpoints) &&
            string.Equals(Channel, other.Channel, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is DeliveryStream other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Endpoints, Channel);
    }

    private readonly struct ScheduledDelivery
    {
        public object Sender { get; }
        public object Receiver { get; }
        public TimeSpan Due { get; }
        public long Sequence { get; }
        public Action Deliver { get; }

        public ScheduledDelivery(
            object sender,
            object receiver,
            TimeSpan due,
            long sequence,
            Action deliver)
        {
            Sender = sender;
            Receiver = receiver;
            Due = due;
            Sequence = sequence;
            Deliver = deliver;
        }
    }
}
