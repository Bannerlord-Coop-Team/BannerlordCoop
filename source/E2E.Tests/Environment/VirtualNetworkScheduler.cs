using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace E2E.Tests.Environment;

public interface IVirtualNetworkScheduler
{
    TimeSpan CurrentTime { get; }
    TimeSpan DefaultLatency { get; set; }
    int PendingDeliveryCount { get; }
    int PendingDeliveryLimit { get; set; }
    int PendingDeliveryHighWaterMark { get; }
    int BackpressureCount { get; }
    IReadOnlyList<VirtualNetworkTraceEntry> Trace { get; }
    IReadOnlyList<VirtualNetworkScheduleInput> RecordedSchedulingInputs { get; }
    IReadOnlyList<VirtualNetworkConnectionTransition> RecordedConnectionTransitions { get; }
    IReadOnlyList<VirtualNetworkLinkTransition> RecordedLinkTransitions { get; }
    IReadOnlyList<VirtualNetworkTimeOperation> RecordedTimeOperations { get; }
    IReadOnlyList<VirtualNetworkDeliveryOperation> RecordedDeliveryOperations { get; }

    void SetLatency(object sender, object receiver, TimeSpan latency);
    void ClearLatency(object sender, object receiver);
    void PauseLink(object sender, object receiver);
    void ResumeLink(object sender, object receiver);
    bool IsLinkPaused(object sender, object receiver);
    long GetConnectionGeneration(object endpoint);
    bool IsConnected(object endpoint);
    VirtualNetworkConnection CaptureConnection(object sender, object receiver);
    int Disconnect(object endpoint);
    void Reconnect(object endpoint);
    void Schedule(object sender, object receiver, string channel, Action delivery);
    void Schedule(VirtualNetworkConnection connection, string channel, Action delivery);
    int Cancel(object endpoint);
    int GetPendingDeliveryCount(object sender, object receiver);
    int AdvanceBy(TimeSpan elapsed);
    int DrainReady();
    int RunUntilIdle();
    VirtualNetworkStateSnapshot CaptureState();
    VirtualNetworkReplay CaptureReplay();
    void Replay(
        VirtualNetworkReplay replay,
        Func<long, object> endpointResolver,
        Action<VirtualNetworkScheduleInput> delivery);
    void ClearTrace();
}

/// <summary>
/// Deterministic virtual-time queue for in-process network tests. Reliable-ordered traffic retains FIFO per
/// directed link and channel, unordered traffic follows virtual due time, and sequenced traffic is newest-wins.
/// </summary>
public class VirtualNetworkScheduler : IVirtualNetworkScheduler
{
    private readonly List<ScheduledDelivery> pending = new();
    private readonly Dictionary<EndpointPair, TimeSpan> linkLatencies = new();
    private readonly HashSet<EndpointPair> pausedLinks = new();
    private readonly Dictionary<DeliveryStream, TimeSpan> lastDueByStream = new();
    private readonly Dictionary<object, EndpointState> endpointStates = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<long, object> endpointsById = new();
    private readonly List<VirtualNetworkTraceEntry> trace = new();
    private readonly List<VirtualNetworkScheduleInput> recordedSchedulingInputs = new();
    private readonly List<VirtualNetworkConnectionTransition> recordedConnectionTransitions = new();
    private readonly List<VirtualNetworkLinkTransition> recordedLinkTransitions = new();
    private readonly List<VirtualNetworkTimeOperation> recordedTimeOperations = new();
    private readonly List<VirtualNetworkDeliveryOperation> recordedDeliveryOperations = new();
    private readonly Stack<ScheduledDelivery> replayDeliveryStack = new();
    private readonly Stack<ReplayTimeOperationContext> replayTimeOperationStack = new();
    private TimeSpan defaultLatency;
    private int pendingDeliveryLimit = int.MaxValue;
    private long nextDeliverySequence;
    private long nextEndpointId = 1;
    private long nextReplaySequence;
    private long nextTraceSequence;

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

    public int PendingDeliveryLimit
    {
        get => pendingDeliveryLimit;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            pendingDeliveryLimit = value;
        }
    }

    public int PendingDeliveryHighWaterMark { get; private set; }
    public int BackpressureCount { get; private set; }
    public IReadOnlyList<VirtualNetworkTraceEntry> Trace => trace;
    public IReadOnlyList<VirtualNetworkScheduleInput> RecordedSchedulingInputs => recordedSchedulingInputs;
    public IReadOnlyList<VirtualNetworkConnectionTransition> RecordedConnectionTransitions =>
        recordedConnectionTransitions;
    public IReadOnlyList<VirtualNetworkLinkTransition> RecordedLinkTransitions => recordedLinkTransitions;
    public IReadOnlyList<VirtualNetworkTimeOperation> RecordedTimeOperations => recordedTimeOperations;
    public IReadOnlyList<VirtualNetworkDeliveryOperation> RecordedDeliveryOperations => recordedDeliveryOperations;

    public void SetLatency(object sender, object receiver, TimeSpan latency)
    {
        ValidateEndpoint(sender, nameof(sender));
        ValidateEndpoint(receiver, nameof(receiver));
        ValidateLatency(latency, nameof(latency));
        RegisterEndpoint(sender);
        RegisterEndpoint(receiver);
        linkLatencies[new EndpointPair(sender, receiver)] = latency;
    }

    public void ClearLatency(object sender, object receiver)
    {
        ValidateEndpoint(sender, nameof(sender));
        ValidateEndpoint(receiver, nameof(receiver));
        linkLatencies.Remove(new EndpointPair(sender, receiver));
    }

    public void PauseLink(object sender, object receiver)
    {
        ValidateEndpoint(sender, nameof(sender));
        ValidateEndpoint(receiver, nameof(receiver));
        EndpointState senderState = RegisterEndpoint(sender);
        EndpointState receiverState = RegisterEndpoint(receiver);

        if (pausedLinks.Add(new EndpointPair(sender, receiver)))
        {
            recordedLinkTransitions.Add(new VirtualNetworkLinkTransition(
                nextReplaySequence++,
                CurrentTime,
                senderState.Id,
                receiverState.Id,
                VirtualNetworkLinkTransitionKind.Paused,
                senderState.Generation,
                receiverState.Generation));
            AddTrace(
                VirtualNetworkTraceKind.LinkPaused,
                senderState.Id,
                receiverState.Id,
                null,
                null,
                senderState.Generation,
                receiverState.Generation,
                null);
        }
    }

    public void ResumeLink(object sender, object receiver)
    {
        ValidateEndpoint(sender, nameof(sender));
        ValidateEndpoint(receiver, nameof(receiver));
        EndpointState senderState = RegisterEndpoint(sender);
        EndpointState receiverState = RegisterEndpoint(receiver);

        if (pausedLinks.Remove(new EndpointPair(sender, receiver)))
        {
            recordedLinkTransitions.Add(new VirtualNetworkLinkTransition(
                nextReplaySequence++,
                CurrentTime,
                senderState.Id,
                receiverState.Id,
                VirtualNetworkLinkTransitionKind.Resumed,
                senderState.Generation,
                receiverState.Generation));
            AddTrace(
                VirtualNetworkTraceKind.LinkResumed,
                senderState.Id,
                receiverState.Id,
                null,
                null,
                senderState.Generation,
                receiverState.Generation,
                null);
        }
    }

    public bool IsLinkPaused(object sender, object receiver)
    {
        ValidateEndpoint(sender, nameof(sender));
        ValidateEndpoint(receiver, nameof(receiver));
        return pausedLinks.Contains(new EndpointPair(sender, receiver));
    }

    public long GetConnectionGeneration(object endpoint)
    {
        ValidateEndpoint(endpoint, nameof(endpoint));
        return RegisterEndpoint(endpoint).Generation;
    }

    public bool IsConnected(object endpoint)
    {
        ValidateEndpoint(endpoint, nameof(endpoint));
        return RegisterEndpoint(endpoint).IsConnected;
    }

    public VirtualNetworkConnection CaptureConnection(object sender, object receiver)
    {
        ValidateEndpoint(sender, nameof(sender));
        ValidateEndpoint(receiver, nameof(receiver));
        EndpointState senderState = RegisterEndpoint(sender);
        EndpointState receiverState = RegisterEndpoint(receiver);

        return new VirtualNetworkConnection(
            this,
            sender,
            receiver,
            senderState.Id,
            receiverState.Id,
            senderState.Generation,
            receiverState.Generation);
    }

    public int Disconnect(object endpoint)
    {
        ValidateEndpoint(endpoint, nameof(endpoint));
        EndpointState state = RegisterEndpoint(endpoint);
        state.Generation++;
        state.IsConnected = false;
        recordedConnectionTransitions.Add(new VirtualNetworkConnectionTransition(
            nextReplaySequence++,
            CurrentTime,
            state.Id,
            VirtualNetworkConnectionTransitionKind.Disconnected,
            state.Generation));
        AddTrace(
            VirtualNetworkTraceKind.Disconnected,
            state.Id,
            null,
            null,
            null,
            state.Generation,
            null,
            null);

        return CancelCore(endpoint);
    }

    public void Reconnect(object endpoint)
    {
        ValidateEndpoint(endpoint, nameof(endpoint));
        EndpointState state = RegisterEndpoint(endpoint);
        CancelCore(endpoint);
        state.Generation++;
        state.IsConnected = true;
        recordedConnectionTransitions.Add(new VirtualNetworkConnectionTransition(
            nextReplaySequence++,
            CurrentTime,
            state.Id,
            VirtualNetworkConnectionTransitionKind.Reconnected,
            state.Generation));
        AddTrace(
            VirtualNetworkTraceKind.Reconnected,
            state.Id,
            null,
            null,
            null,
            state.Generation,
            null,
            null);
    }

    public void Schedule(object sender, object receiver, string channel, Action delivery)
    {
        Schedule(CaptureConnection(sender, receiver), channel, delivery);
    }

    public void Schedule(VirtualNetworkConnection connection, string channel, Action delivery)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        if (!ReferenceEquals(connection.Owner, this))
            throw new ArgumentException("The connection belongs to another scheduler", nameof(connection));
        if (string.IsNullOrEmpty(channel)) throw new ArgumentException("A channel is required", nameof(channel));
        if (delivery == null) throw new ArgumentNullException(nameof(delivery));

        if (!IsCurrent(connection))
        {
            AddTrace(
                VirtualNetworkTraceKind.StaleConnectionRejected,
                connection.SenderEndpointId,
                connection.ReceiverEndpointId,
                channel,
                null,
                connection.SenderGeneration,
                connection.ReceiverGeneration,
                "connection generation changed");
            throw new VirtualNetworkStaleConnectionException();
        }

        EndpointState senderState = RegisterEndpoint(connection.Sender);
        EndpointState receiverState = RegisterEndpoint(connection.Receiver);
        if (!senderState.IsConnected || !receiverState.IsConnected)
        {
            AddTrace(
                VirtualNetworkTraceKind.DisconnectedSendRejected,
                senderState.Id,
                receiverState.Id,
                channel,
                null,
                senderState.Generation,
                receiverState.Generation,
                "endpoint is disconnected");
            throw new InvalidOperationException("Traffic cannot be scheduled for a disconnected endpoint");
        }

        var endpoints = new EndpointPair(connection.Sender, connection.Receiver);
        TimeSpan latency = linkLatencies.TryGetValue(endpoints, out TimeSpan configuredLatency)
            ? configuredLatency
            : DefaultLatency;
        TimeSpan due = CurrentTime + latency;
        var stream = new DeliveryStream(endpoints, channel);
        DeliverySemantics semantics = GetDeliverySemantics(channel);

        if (semantics == DeliverySemantics.ReliableOrdered &&
            lastDueByStream.TryGetValue(stream, out TimeSpan previousDue) &&
            due < previousDue)
        {
            due = previousDue;
        }

        if (semantics == DeliverySemantics.Sequenced)
            SupersedePending(stream, due);

        ScheduleCore(connection, channel, delivery, CurrentTime, due, stream, null);
    }

    public int Cancel(object endpoint)
    {
        ValidateEndpoint(endpoint, nameof(endpoint));
        EndpointState state = RegisterEndpoint(endpoint);
        recordedConnectionTransitions.Add(new VirtualNetworkConnectionTransition(
            nextReplaySequence++,
            CurrentTime,
            state.Id,
            VirtualNetworkConnectionTransitionKind.Canceled,
            state.Generation));
        return CancelCore(endpoint);
    }

    public int GetPendingDeliveryCount(object sender, object receiver)
    {
        ValidateEndpoint(sender, nameof(sender));
        ValidateEndpoint(receiver, nameof(receiver));
        return pending.Count(delivery =>
            ReferenceEquals(delivery.Sender, sender) &&
            ReferenceEquals(delivery.Receiver, receiver));
    }

    public int AdvanceBy(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        return ExecuteTimeOperation(VirtualNetworkTimeOperationKind.AdvanceBy, elapsed);
    }

    public int DrainReady() => ExecuteTimeOperation(
        VirtualNetworkTimeOperationKind.DrainReady,
        TimeSpan.Zero);

    private int DrainReadyCore()
    {
        int delivered = 0;

        while (TryTakeNext(CurrentTime, out ScheduledDelivery next))
        {
            RefreshStreamTail(next.Stream);
            recordedDeliveryOperations.Add(new VirtualNetworkDeliveryOperation(
                nextReplaySequence++,
                CurrentTime,
                VirtualNetworkDeliveryOperationKind.Started,
                next.Sequence));
            try
            {
                next.Deliver();
                delivered++;
                recordedDeliveryOperations.Add(new VirtualNetworkDeliveryOperation(
                    nextReplaySequence++,
                    CurrentTime,
                    VirtualNetworkDeliveryOperationKind.Completed,
                    next.Sequence));
                AddTrace(
                    VirtualNetworkTraceKind.Delivered,
                    next.SenderEndpointId,
                    next.ReceiverEndpointId,
                    next.Stream.Channel,
                    next.Sequence,
                    next.SenderGeneration,
                    next.ReceiverGeneration,
                    null);
            }
            catch
            {
                recordedDeliveryOperations.Add(new VirtualNetworkDeliveryOperation(
                    nextReplaySequence++,
                    CurrentTime,
                    VirtualNetworkDeliveryOperationKind.Failed,
                    next.Sequence));
                AddTrace(
                    VirtualNetworkTraceKind.DeliveryFailed,
                    next.SenderEndpointId,
                    next.ReceiverEndpointId,
                    next.Stream.Channel,
                    next.Sequence,
                    next.SenderGeneration,
                    next.ReceiverGeneration,
                    "delivery callback threw");
                throw;
            }
        }

        return delivered;
    }

    public int RunUntilIdle() => ExecuteTimeOperation(
        VirtualNetworkTimeOperationKind.RunUntilIdle,
        TimeSpan.Zero);

    private int ExecuteTimeOperation(VirtualNetworkTimeOperationKind kind, TimeSpan elapsed)
    {
        RecordTimeOperation(kind, VirtualNetworkTimeOperationPhase.Started, elapsed);
        try
        {
            int delivered;
            switch (kind)
            {
                case VirtualNetworkTimeOperationKind.AdvanceBy:
                    CurrentTime += elapsed;
                    delivered = DrainReadyCore();
                    break;
                case VirtualNetworkTimeOperationKind.DrainReady:
                    delivered = DrainReadyCore();
                    break;
                case VirtualNetworkTimeOperationKind.RunUntilIdle:
                    delivered = RunUntilIdleCore();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }

            RecordTimeOperation(kind, VirtualNetworkTimeOperationPhase.Completed, elapsed);
            return delivered;
        }
        catch
        {
            RecordTimeOperation(kind, VirtualNetworkTimeOperationPhase.Failed, elapsed);
            throw;
        }
    }

    private int RunUntilIdleCore()
    {
        int delivered = 0;
        while (TryGetNextUnpausedDue(out TimeSpan nextDue))
        {
            if (nextDue > CurrentTime)
                CurrentTime = nextDue;

            delivered += DrainReadyCore();
        }

        return delivered;
    }

    private void RecordTimeOperation(
        VirtualNetworkTimeOperationKind kind,
        VirtualNetworkTimeOperationPhase phase,
        TimeSpan elapsed)
    {
        recordedTimeOperations.Add(new VirtualNetworkTimeOperation(
            nextReplaySequence++,
            CurrentTime,
            kind,
            phase,
            elapsed));
    }

    public VirtualNetworkStateSnapshot CaptureState()
    {
        VirtualNetworkEndpointState[] endpoints = endpointStates.Values
            .OrderBy(state => state.Id)
            .Select(state => new VirtualNetworkEndpointState(
                state.Id,
                state.Generation,
                state.IsConnected))
            .ToArray();
        VirtualNetworkLinkLatencyState[] latencies = linkLatencies
            .Select(item => new VirtualNetworkLinkLatencyState(
                endpointStates[item.Key.Sender].Id,
                endpointStates[item.Key.Receiver].Id,
                item.Value.Ticks))
            .OrderBy(item => item.SenderEndpointId)
            .ThenBy(item => item.ReceiverEndpointId)
            .ToArray();
        VirtualNetworkPausedLinkState[] partitions = pausedLinks
            .Select(item => new VirtualNetworkPausedLinkState(
                endpointStates[item.Sender].Id,
                endpointStates[item.Receiver].Id))
            .OrderBy(item => item.SenderEndpointId)
            .ThenBy(item => item.ReceiverEndpointId)
            .ToArray();
        VirtualNetworkPendingDeliveryState[] pendingDeliveries = pending
            .OrderBy(delivery => delivery.Sequence)
            .Select(delivery => new VirtualNetworkPendingDeliveryState(
                delivery.Sequence,
                delivery.SenderEndpointId,
                delivery.ReceiverEndpointId,
                delivery.SenderGeneration,
                delivery.ReceiverGeneration,
                delivery.Stream.Channel,
                delivery.Due.Ticks))
            .ToArray();

        var stateMaterial = new
        {
            SchemaVersion = VirtualNetworkStateSnapshot.CurrentSchemaVersion,
            CurrentTimeTicks = CurrentTime.Ticks,
            DefaultLatencyTicks = DefaultLatency.Ticks,
            PendingDeliveryLimit,
            PendingDeliveryHighWaterMark,
            BackpressureCount,
            Endpoints = endpoints,
            LinkLatencies = latencies,
            PausedLinks = partitions,
            PendingDeliveries = pendingDeliveries,
        };
        string json = JsonSerializer.Serialize(stateMaterial);
        string stateDigest = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();

        return new VirtualNetworkStateSnapshot(
            CurrentTime.Ticks,
            DefaultLatency.Ticks,
            PendingDeliveryLimit,
            PendingDeliveryHighWaterMark,
            BackpressureCount,
            endpoints,
            latencies,
            partitions,
            pendingDeliveries,
            stateDigest);
    }

    public VirtualNetworkReplay CaptureReplay() => new(
        recordedSchedulingInputs.ToArray(),
        recordedConnectionTransitions.ToArray(),
        recordedLinkTransitions.ToArray(),
        recordedTimeOperations.ToArray(),
        recordedDeliveryOperations.ToArray());

    public void Replay(
        VirtualNetworkReplay replay,
        Func<long, object> endpointResolver,
        Action<VirtualNetworkScheduleInput> delivery)
    {
        if (replay == null) throw new ArgumentNullException(nameof(replay));
        if (endpointResolver == null) throw new ArgumentNullException(nameof(endpointResolver));
        if (delivery == null) throw new ArgumentNullException(nameof(delivery));
        ValidateFreshReplayTarget();

        int scheduleIndex = 0;
        int transitionIndex = 0;
        int linkTransitionIndex = 0;
        int timeOperationIndex = 0;
        int deliveryOperationIndex = 0;
        long expectedReplaySequence = 0;
        while (scheduleIndex < replay.SchedulingInputs.Count ||
               transitionIndex < replay.ConnectionTransitions.Count ||
               linkTransitionIndex < replay.LinkTransitions.Count ||
               timeOperationIndex < replay.TimeOperations.Count ||
               deliveryOperationIndex < replay.DeliveryOperations.Count)
        {
            VirtualNetworkScheduleInput? schedule = scheduleIndex < replay.SchedulingInputs.Count
                ? replay.SchedulingInputs[scheduleIndex]
                : null;
            VirtualNetworkConnectionTransition? transition =
                transitionIndex < replay.ConnectionTransitions.Count
                    ? replay.ConnectionTransitions[transitionIndex]
                    : null;
            VirtualNetworkLinkTransition? linkTransition =
                linkTransitionIndex < replay.LinkTransitions.Count
                    ? replay.LinkTransitions[linkTransitionIndex]
                    : null;
            VirtualNetworkTimeOperation? timeOperation =
                timeOperationIndex < replay.TimeOperations.Count
                    ? replay.TimeOperations[timeOperationIndex]
                    : null;
            VirtualNetworkDeliveryOperation? deliveryOperation =
                deliveryOperationIndex < replay.DeliveryOperations.Count
                    ? replay.DeliveryOperations[deliveryOperationIndex]
                    : null;

            long nextSequence = new[]
            {
                schedule?.ReplaySequence ?? long.MaxValue,
                transition?.ReplaySequence ?? long.MaxValue,
                linkTransition?.ReplaySequence ?? long.MaxValue,
                timeOperation?.ReplaySequence ?? long.MaxValue,
                deliveryOperation?.ReplaySequence ?? long.MaxValue,
            }.Min();
            if (nextSequence != expectedReplaySequence)
                throw new ArgumentException("Replay operations must be complete and in recorded order", nameof(replay));

            if (schedule != null && schedule.ReplaySequence == nextSequence)
            {
                if (schedule.InputSequence != scheduleIndex || schedule.DeliverySequence != scheduleIndex)
                    throw new ArgumentException("Scheduling inputs must be complete and in recorded order", nameof(replay));

                ReplaySchedule(schedule, endpointResolver, delivery, nameof(replay));
                scheduleIndex++;
            }
            else if (transition != null && transition.ReplaySequence == nextSequence)
            {
                ReplayConnectionTransition(transition, endpointResolver, nameof(replay));
                transitionIndex++;
            }
            else if (linkTransition != null && linkTransition.ReplaySequence == nextSequence)
            {
                ReplayLinkTransition(linkTransition, endpointResolver, nameof(replay));
                linkTransitionIndex++;
            }
            else if (timeOperation != null && timeOperation.ReplaySequence == nextSequence)
            {
                ReplayTimeOperation(timeOperation, nameof(replay));
                timeOperationIndex++;
            }
            else if (deliveryOperation != null && deliveryOperation.ReplaySequence == nextSequence)
            {
                ReplayDeliveryOperation(deliveryOperation, nameof(replay));
                deliveryOperationIndex++;
            }
            else
            {
                throw new ArgumentException("Replay operations must be complete and in recorded order", nameof(replay));
            }

            expectedReplaySequence++;
        }

        if (replayDeliveryStack.Count != 0)
            throw new ArgumentException("Replay delivery operations are incomplete", nameof(replay));
        if (replayTimeOperationStack.Count != 0)
            throw new ArgumentException("Replay time operations are incomplete", nameof(replay));
    }

    private void ReplaySchedule(
        VirtualNetworkScheduleInput input,
        Func<long, object> endpointResolver,
        Action<VirtualNetworkScheduleInput> delivery,
        string parameterName)
    {
        if (input.ScheduledAt < CurrentTime || input.DueAt < input.ScheduledAt)
            throw new ArgumentException("Scheduling input times are invalid", parameterName);
        if (string.IsNullOrEmpty(input.Channel))
            throw new ArgumentException("Every scheduling input must have a channel", parameterName);

        object sender = endpointResolver(input.SenderEndpointId);
        object receiver = endpointResolver(input.ReceiverEndpointId);
        ValidateEndpoint(sender, nameof(endpointResolver));
        ValidateEndpoint(receiver, nameof(endpointResolver));
        EndpointState senderState = BindReplayEndpoint(
            sender,
            input.SenderEndpointId,
            input.SenderGeneration,
            parameterName);
        EndpointState receiverState = BindReplayEndpoint(
            receiver,
            input.ReceiverEndpointId,
            input.ReceiverGeneration,
            parameterName);
        if (!senderState.IsConnected || !receiverState.IsConnected)
            throw new ArgumentException("A scheduling input uses a disconnected endpoint", parameterName);

        CurrentTime = input.ScheduledAt;
        var connection = new VirtualNetworkConnection(
            this,
            sender,
            receiver,
            senderState.Id,
            receiverState.Id,
            senderState.Generation,
            receiverState.Generation);
        var stream = new DeliveryStream(new EndpointPair(sender, receiver), input.Channel);
        DeliverySemantics semantics = GetDeliverySemantics(input.Channel);
        if (semantics == DeliverySemantics.ReliableOrdered &&
            lastDueByStream.TryGetValue(stream, out TimeSpan previousDue) &&
            input.DueAt < previousDue)
        {
            throw new ArgumentException("A replay input violates stream FIFO ordering", parameterName);
        }

        if (semantics == DeliverySemantics.Sequenced)
            SupersedePending(stream, input.DueAt);

        ScheduleCore(
            connection,
            input.Channel,
            () => delivery(input),
            input.ScheduledAt,
            input.DueAt,
            stream,
            input);
    }

    private void ReplayConnectionTransition(
        VirtualNetworkConnectionTransition transition,
        Func<long, object> endpointResolver,
        string parameterName)
    {
        if (transition.Time < CurrentTime)
            throw new ArgumentException("Connection transition times are invalid", parameterName);
        if (transition.ResultingGeneration < 0)
            throw new ArgumentException("Connection transition generations are invalid", parameterName);

        object endpoint = endpointResolver(transition.EndpointId);
        ValidateEndpoint(endpoint, nameof(endpointResolver));
        long initialGeneration = transition.Kind == VirtualNetworkConnectionTransitionKind.Canceled
            ? transition.ResultingGeneration
            : transition.ResultingGeneration - 1;
        if (initialGeneration < 0)
            throw new ArgumentException("Connection transition generations are invalid", parameterName);

        EndpointState state = BindReplayEndpoint(
            endpoint,
            transition.EndpointId,
            initialGeneration,
            parameterName);
        CurrentTime = transition.Time;

        switch (transition.Kind)
        {
            case VirtualNetworkConnectionTransitionKind.Disconnected:
                Disconnect(endpoint);
                break;
            case VirtualNetworkConnectionTransitionKind.Reconnected:
                Reconnect(endpoint);
                break;
            case VirtualNetworkConnectionTransitionKind.Canceled:
                Cancel(endpoint);
                break;
            default:
                throw new ArgumentOutOfRangeException(parameterName);
        }

        if (recordedConnectionTransitions[^1] != transition ||
            state.Generation != transition.ResultingGeneration)
        {
            throw new ArgumentException("A connection transition is inconsistent with prior replay state", parameterName);
        }
    }

    private void ReplayLinkTransition(
        VirtualNetworkLinkTransition transition,
        Func<long, object> endpointResolver,
        string parameterName)
    {
        if (transition.Time < CurrentTime)
            throw new ArgumentException("Link transition times are invalid", parameterName);

        object sender = endpointResolver(transition.SenderEndpointId);
        object receiver = endpointResolver(transition.ReceiverEndpointId);
        ValidateEndpoint(sender, nameof(endpointResolver));
        ValidateEndpoint(receiver, nameof(endpointResolver));
        BindReplayEndpoint(
            sender,
            transition.SenderEndpointId,
            transition.SenderGeneration,
            parameterName);
        BindReplayEndpoint(
            receiver,
            transition.ReceiverEndpointId,
            transition.ReceiverGeneration,
            parameterName);
        CurrentTime = transition.Time;

        int priorTransitionCount = recordedLinkTransitions.Count;
        switch (transition.Kind)
        {
            case VirtualNetworkLinkTransitionKind.Paused:
                PauseLink(sender, receiver);
                break;
            case VirtualNetworkLinkTransitionKind.Resumed:
                ResumeLink(sender, receiver);
                break;
            default:
                throw new ArgumentOutOfRangeException(parameterName);
        }

        if (recordedLinkTransitions.Count != priorTransitionCount + 1 ||
            recordedLinkTransitions[^1] != transition)
        {
            throw new ArgumentException("A link transition is inconsistent with prior replay state", parameterName);
        }
    }

    private void ReplayTimeOperation(VirtualNetworkTimeOperation operation, string parameterName)
    {
        if (operation.ReplaySequence != nextReplaySequence ||
            operation.Elapsed < TimeSpan.Zero)
        {
            throw new ArgumentException("Time operations are inconsistent with prior replay state", parameterName);
        }

        if (operation.Phase == VirtualNetworkTimeOperationPhase.Started)
        {
            if (operation.Time != CurrentTime)
                throw new ArgumentException("Time operations are inconsistent with prior replay state", parameterName);
            ValidateTimeOperation(operation, parameterName);
            recordedTimeOperations.Add(operation);
            nextReplaySequence++;
            replayTimeOperationStack.Push(new ReplayTimeOperationContext(
                operation,
                replayDeliveryStack.Count));
            if (operation.Kind == VirtualNetworkTimeOperationKind.AdvanceBy)
                CurrentTime += operation.Elapsed;
            return;
        }

        if (operation.Phase != VirtualNetworkTimeOperationPhase.Completed &&
            operation.Phase != VirtualNetworkTimeOperationPhase.Failed)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
        if (operation.Time != CurrentTime || replayTimeOperationStack.Count == 0)
            throw new ArgumentException("A time-operation result has no matching invocation", parameterName);

        ReplayTimeOperationContext context = replayTimeOperationStack.Peek();
        if (context.Operation.Kind != operation.Kind ||
            context.Operation.Elapsed != operation.Elapsed ||
            context.DeliveryDepthAtStart != replayDeliveryStack.Count)
        {
            throw new ArgumentException("A time-operation result does not match its invocation", parameterName);
        }

        replayTimeOperationStack.Pop();
        recordedTimeOperations.Add(operation);
        nextReplaySequence++;
    }

    private void ReplayDeliveryOperation(VirtualNetworkDeliveryOperation operation, string parameterName)
    {
        if (operation.ReplaySequence != nextReplaySequence || replayTimeOperationStack.Count == 0)
        {
            throw new ArgumentException("Delivery operations are inconsistent with prior replay state", parameterName);
        }

        if (operation.Kind == VirtualNetworkDeliveryOperationKind.Started)
        {
            VirtualNetworkTimeOperationKind timeOperationKind = replayTimeOperationStack.Peek().Operation.Kind;
            if (timeOperationKind == VirtualNetworkTimeOperationKind.RunUntilIdle)
            {
                if (!TryGetNextUnpausedDue(out TimeSpan nextDue))
                    throw new ArgumentException("Run-until-idle has no pending delivery", parameterName);
                TimeSpan expectedTime = nextDue > CurrentTime ? nextDue : CurrentTime;
                if (operation.Time != expectedTime)
                    throw new ArgumentException("Run-until-idle advanced to an invalid time", parameterName);
                CurrentTime = expectedTime;
            }
            else if (operation.Time != CurrentTime)
            {
                throw new ArgumentException("A drain delivery cannot advance virtual time", parameterName);
            }

            if (!TryTakeNext(CurrentTime, out ScheduledDelivery next) ||
                next.Sequence != operation.DeliverySequence)
            {
                throw new ArgumentException("A delivery operation does not match pending traffic", parameterName);
            }

            RefreshStreamTail(next.Stream);
            recordedDeliveryOperations.Add(operation);
            nextReplaySequence++;
            replayDeliveryStack.Push(next);
            try
            {
                next.Deliver();
            }
            catch
            {
                replayDeliveryStack.Pop();
                AddTrace(
                    VirtualNetworkTraceKind.DeliveryFailed,
                    next.SenderEndpointId,
                    next.ReceiverEndpointId,
                    next.Stream.Channel,
                    next.Sequence,
                    next.SenderGeneration,
                    next.ReceiverGeneration,
                    "delivery callback threw");
                throw;
            }
            return;
        }

        if (operation.Time != CurrentTime ||
            replayDeliveryStack.Count == 0 ||
            replayDeliveryStack.Peek().Sequence != operation.DeliverySequence)
        {
            throw new ArgumentException("A delivery completion does not match the active delivery", parameterName);
        }

        ScheduledDelivery active = replayDeliveryStack.Pop();
        recordedDeliveryOperations.Add(operation);
        nextReplaySequence++;
        if (operation.Kind == VirtualNetworkDeliveryOperationKind.Completed)
        {
            AddTrace(
                VirtualNetworkTraceKind.Delivered,
                active.SenderEndpointId,
                active.ReceiverEndpointId,
                active.Stream.Channel,
                active.Sequence,
                active.SenderGeneration,
                active.ReceiverGeneration,
                null);
            return;
        }

        if (operation.Kind == VirtualNetworkDeliveryOperationKind.Failed)
        {
            AddTrace(
                VirtualNetworkTraceKind.DeliveryFailed,
                active.SenderEndpointId,
                active.ReceiverEndpointId,
                active.Stream.Channel,
                active.Sequence,
                active.SenderGeneration,
                active.ReceiverGeneration,
                "delivery callback threw");
            return;
        }

        throw new ArgumentOutOfRangeException(parameterName);
    }

    private static void ValidateTimeOperation(VirtualNetworkTimeOperation operation, string parameterName)
    {
        switch (operation.Kind)
        {
            case VirtualNetworkTimeOperationKind.AdvanceBy:
                return;
            case VirtualNetworkTimeOperationKind.DrainReady:
                if (operation.Elapsed != TimeSpan.Zero)
                    throw new ArgumentException("Drain operations cannot advance time", parameterName);
                return;
            case VirtualNetworkTimeOperationKind.RunUntilIdle:
                if (operation.Elapsed != TimeSpan.Zero)
                    throw new ArgumentException("Run-until-idle operations cannot specify elapsed time", parameterName);
                return;
            default:
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private EndpointState BindReplayEndpoint(
        object endpoint,
        long endpointId,
        long generation,
        string parameterName)
    {
        if (endpointId <= 0 || generation < 0)
            throw new ArgumentException("Replay endpoint identity is invalid", parameterName);

        if (endpointStates.TryGetValue(endpoint, out EndpointState? existingState))
        {
            if (existingState.Id != endpointId || existingState.Generation != generation)
                throw new ArgumentException("A replay endpoint changed id or generation without a transition", parameterName);
            return existingState;
        }

        if (endpointsById.TryGetValue(endpointId, out object? existingEndpoint) &&
            !ReferenceEquals(existingEndpoint, endpoint))
        {
            throw new ArgumentException("A replay endpoint id resolved to multiple objects", parameterName);
        }

        var state = new EndpointState(endpointId)
        {
            Generation = generation,
        };
        endpointStates.Add(endpoint, state);
        endpointsById[endpointId] = endpoint;
        if (endpointId >= nextEndpointId)
            nextEndpointId = endpointId + 1;
        return state;
    }

    private void ValidateFreshReplayTarget()
    {
        if (pending.Count != 0 ||
            recordedSchedulingInputs.Count != 0 ||
            recordedConnectionTransitions.Count != 0 ||
            recordedLinkTransitions.Count != 0 ||
            recordedTimeOperations.Count != 0 ||
            recordedDeliveryOperations.Count != 0 ||
            replayDeliveryStack.Count != 0 ||
            replayTimeOperationStack.Count != 0 ||
            endpointStates.Count != 0 ||
            pausedLinks.Count != 0 ||
            linkLatencies.Count != 0 ||
            CurrentTime != TimeSpan.Zero)
        {
            throw new InvalidOperationException("Inputs can only be replayed into a fresh scheduler");
        }
    }

    public void ClearTrace()
    {
        trace.Clear();
        nextTraceSequence = 0;
    }

    private void ScheduleCore(
        VirtualNetworkConnection connection,
        string channel,
        Action delivery,
        TimeSpan scheduledAt,
        TimeSpan due,
        DeliveryStream stream,
        VirtualNetworkScheduleInput? replayInput)
    {
        if (pending.Count >= PendingDeliveryLimit)
        {
            BackpressureCount++;
            AddTrace(
                VirtualNetworkTraceKind.Backpressure,
                connection.SenderEndpointId,
                connection.ReceiverEndpointId,
                channel,
                null,
                connection.SenderGeneration,
                connection.ReceiverGeneration,
                $"pending limit {PendingDeliveryLimit}");
            throw new VirtualNetworkBackpressureException(PendingDeliveryLimit);
        }

        long sequence = nextDeliverySequence++;
        var input = replayInput ?? new VirtualNetworkScheduleInput(
            recordedSchedulingInputs.Count,
            nextReplaySequence++,
            sequence,
            connection.SenderEndpointId,
            connection.ReceiverEndpointId,
            channel,
            scheduledAt,
            due,
            connection.SenderGeneration,
            connection.ReceiverGeneration);
        if (input.InputSequence != recordedSchedulingInputs.Count || input.DeliverySequence != sequence)
            throw new ArgumentException("The replay input sequence does not match the scheduler", nameof(replayInput));
        if (replayInput != null && input.ReplaySequence >= nextReplaySequence)
            nextReplaySequence = input.ReplaySequence + 1;

        recordedSchedulingInputs.Add(input);
        if (GetDeliverySemantics(channel) == DeliverySemantics.ReliableOrdered)
            lastDueByStream[stream] = due;
        pending.Add(new ScheduledDelivery(
            connection.Sender,
            connection.Receiver,
            connection.SenderEndpointId,
            connection.ReceiverEndpointId,
            connection.SenderGeneration,
            connection.ReceiverGeneration,
            stream,
            due,
            sequence,
            delivery));
        if (pending.Count > PendingDeliveryHighWaterMark)
            PendingDeliveryHighWaterMark = pending.Count;

        AddTrace(
            VirtualNetworkTraceKind.Scheduled,
            connection.SenderEndpointId,
            connection.ReceiverEndpointId,
            channel,
            sequence,
            connection.SenderGeneration,
            connection.ReceiverGeneration,
            null);
    }

    private int CancelCore(object endpoint)
    {
        ScheduledDelivery[] canceled = pending
            .Where(delivery =>
                ReferenceEquals(delivery.Sender, endpoint) ||
                ReferenceEquals(delivery.Receiver, endpoint))
            .OrderBy(delivery => delivery.Sequence)
            .ToArray();

        foreach (ScheduledDelivery delivery in canceled)
        {
            pending.Remove(delivery);
            AddTrace(
                VirtualNetworkTraceKind.Canceled,
                delivery.SenderEndpointId,
                delivery.ReceiverEndpointId,
                delivery.Stream.Channel,
                delivery.Sequence,
                delivery.SenderGeneration,
                delivery.ReceiverGeneration,
                "endpoint canceled");
        }

        var affectedStreams = lastDueByStream.Keys
            .Where(stream =>
                ReferenceEquals(stream.Endpoints.Sender, endpoint) ||
                ReferenceEquals(stream.Endpoints.Receiver, endpoint))
            .ToArray();
        foreach (DeliveryStream stream in affectedStreams)
            lastDueByStream.Remove(stream);

        return canceled.Length;
    }

    private void SupersedePending(DeliveryStream stream, TimeSpan arrivingDue)
    {
        ScheduledDelivery[] superseded = pending
            .Where(delivery =>
                delivery.Stream.Equals(stream) &&
                delivery.Due > arrivingDue)
            .OrderBy(delivery => delivery.Sequence)
            .ToArray();

        foreach (ScheduledDelivery delivery in superseded)
        {
            pending.Remove(delivery);
            AddTrace(
                VirtualNetworkTraceKind.Superseded,
                delivery.SenderEndpointId,
                delivery.ReceiverEndpointId,
                delivery.Stream.Channel,
                delivery.Sequence,
                delivery.SenderGeneration,
                delivery.ReceiverGeneration,
                "newer sequenced delivery scheduled");
        }

        lastDueByStream.Remove(stream);
    }

    private bool TryTakeNext(TimeSpan maximumDue, out ScheduledDelivery next)
    {
        int nextIndex = -1;
        next = default;

        for (int i = 0; i < pending.Count; i++)
        {
            ScheduledDelivery candidate = pending[i];
            if (candidate.Due > maximumDue || pausedLinks.Contains(candidate.Stream.Endpoints)) continue;
            if (nextIndex >= 0 && Compare(candidate, next) >= 0) continue;

            nextIndex = i;
            next = candidate;
        }

        if (nextIndex < 0) return false;

        pending.RemoveAt(nextIndex);
        return true;
    }

    private bool TryGetNextUnpausedDue(out TimeSpan due)
    {
        bool found = false;
        due = default;

        foreach (ScheduledDelivery delivery in pending)
        {
            if (pausedLinks.Contains(delivery.Stream.Endpoints)) continue;
            if (found && delivery.Due >= due) continue;

            due = delivery.Due;
            found = true;
        }

        return found;
    }

    private void RefreshStreamTail(DeliveryStream stream)
    {
        if (GetDeliverySemantics(stream.Channel) != DeliverySemantics.ReliableOrdered)
        {
            lastDueByStream.Remove(stream);
            return;
        }

        bool found = false;
        TimeSpan latestDue = default;

        foreach (ScheduledDelivery delivery in pending)
        {
            if (!delivery.Stream.Equals(stream)) continue;
            if (found && delivery.Due <= latestDue) continue;

            latestDue = delivery.Due;
            found = true;
        }

        if (found)
            lastDueByStream[stream] = latestDue;
        else
            lastDueByStream.Remove(stream);
    }

    private bool IsCurrent(VirtualNetworkConnection connection)
    {
        EndpointState senderState = RegisterEndpoint(connection.Sender);
        EndpointState receiverState = RegisterEndpoint(connection.Receiver);
        return senderState.Generation == connection.SenderGeneration &&
               receiverState.Generation == connection.ReceiverGeneration;
    }

    private EndpointState RegisterEndpoint(object endpoint)
    {
        if (endpointStates.TryGetValue(endpoint, out EndpointState? state)) return state;

        while (endpointsById.ContainsKey(nextEndpointId))
            nextEndpointId++;
        state = new EndpointState(nextEndpointId++);
        endpointStates.Add(endpoint, state);
        endpointsById.Add(state.Id, endpoint);
        return state;
    }

    private void AddTrace(
        VirtualNetworkTraceKind kind,
        long? senderEndpointId,
        long? receiverEndpointId,
        string? channel,
        long? deliverySequence,
        long? senderGeneration,
        long? receiverGeneration,
        string? detail)
    {
        trace.Add(new VirtualNetworkTraceEntry(
            nextTraceSequence++,
            CurrentTime,
            kind,
            senderEndpointId,
            receiverEndpointId,
            channel,
            deliverySequence,
            senderGeneration,
            receiverGeneration,
            pending.Count,
            detail));
    }

    private static int Compare(ScheduledDelivery first, ScheduledDelivery second)
    {
        int dueComparison = first.Due.CompareTo(second.Due);
        return dueComparison != 0
            ? dueComparison
            : first.Sequence.CompareTo(second.Sequence);
    }

    private static DeliverySemantics GetDeliverySemantics(string channel)
    {
        string deliveryMethod = channel;
        int separator = channel.IndexOf(':');
        if (separator >= 0)
        {
            string prefix = channel[..separator];
            string suffix = channel[(separator + 1)..];
            deliveryMethod = string.Equals(prefix, "packet", StringComparison.Ordinal)
                ? suffix
                : prefix;
        }

        if (string.Equals(deliveryMethod, "message", StringComparison.Ordinal) ||
            string.Equals(deliveryMethod, "ReliableOrdered", StringComparison.Ordinal))
        {
            return DeliverySemantics.ReliableOrdered;
        }

        if (string.Equals(deliveryMethod, "Sequenced", StringComparison.Ordinal) ||
            string.Equals(deliveryMethod, "ReliableSequenced", StringComparison.Ordinal))
        {
            return DeliverySemantics.Sequenced;
        }

        return DeliverySemantics.Unordered;
    }

    private static void ValidateEndpoint(object endpoint, string parameterName)
    {
        if (endpoint == null) throw new ArgumentNullException(parameterName);
    }

    private static void ValidateLatency(TimeSpan latency, string parameterName)
    {
        if (latency < TimeSpan.Zero) throw new ArgumentOutOfRangeException(parameterName);
    }

    private sealed class ReplayTimeOperationContext
    {
        public VirtualNetworkTimeOperation Operation { get; }
        public int DeliveryDepthAtStart { get; }

        public ReplayTimeOperationContext(
            VirtualNetworkTimeOperation operation,
            int deliveryDepthAtStart)
        {
            Operation = operation;
            DeliveryDepthAtStart = deliveryDepthAtStart;
        }
    }

    private sealed class EndpointState
    {
        public long Id { get; }
        public long Generation { get; set; }
        public bool IsConnected { get; set; } = true;

        public EndpointState(long id)
        {
            Id = id;
        }
    }

    private enum DeliverySemantics
    {
        ReliableOrdered,
        Unordered,
        Sequenced,
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

    private readonly struct ScheduledDelivery : IEquatable<ScheduledDelivery>
    {
        public object Sender { get; }
        public object Receiver { get; }
        public long SenderEndpointId { get; }
        public long ReceiverEndpointId { get; }
        public long SenderGeneration { get; }
        public long ReceiverGeneration { get; }
        public DeliveryStream Stream { get; }
        public TimeSpan Due { get; }
        public long Sequence { get; }
        public Action Deliver { get; }

        public ScheduledDelivery(
            object sender,
            object receiver,
            long senderEndpointId,
            long receiverEndpointId,
            long senderGeneration,
            long receiverGeneration,
            DeliveryStream stream,
            TimeSpan due,
            long sequence,
            Action deliver)
        {
            Sender = sender;
            Receiver = receiver;
            SenderEndpointId = senderEndpointId;
            ReceiverEndpointId = receiverEndpointId;
            SenderGeneration = senderGeneration;
            ReceiverGeneration = receiverGeneration;
            Stream = stream;
            Due = due;
            Sequence = sequence;
            Deliver = deliver;
        }

        public bool Equals(ScheduledDelivery other) => Sequence == other.Sequence;
        public override bool Equals(object? obj) => obj is ScheduledDelivery other && Equals(other);
        public override int GetHashCode() => Sequence.GetHashCode();
    }
}

public sealed class VirtualNetworkConnection
{
    internal object Owner { get; }
    internal object Sender { get; }
    internal object Receiver { get; }
    public long SenderEndpointId { get; }
    public long ReceiverEndpointId { get; }
    public long SenderGeneration { get; }
    public long ReceiverGeneration { get; }

    internal VirtualNetworkConnection(
        object owner,
        object sender,
        object receiver,
        long senderEndpointId,
        long receiverEndpointId,
        long senderGeneration,
        long receiverGeneration)
    {
        Owner = owner;
        Sender = sender;
        Receiver = receiver;
        SenderEndpointId = senderEndpointId;
        ReceiverEndpointId = receiverEndpointId;
        SenderGeneration = senderGeneration;
        ReceiverGeneration = receiverGeneration;
    }
}

public sealed record VirtualNetworkScheduleInput(
    long InputSequence,
    long ReplaySequence,
    long DeliverySequence,
    long SenderEndpointId,
    long ReceiverEndpointId,
    string Channel,
    TimeSpan ScheduledAt,
    TimeSpan DueAt,
    long SenderGeneration,
    long ReceiverGeneration);

public enum VirtualNetworkConnectionTransitionKind
{
    Disconnected,
    Reconnected,
    Canceled,
}

public sealed record VirtualNetworkConnectionTransition(
    long ReplaySequence,
    TimeSpan Time,
    long EndpointId,
    VirtualNetworkConnectionTransitionKind Kind,
    long ResultingGeneration);

public enum VirtualNetworkLinkTransitionKind
{
    Paused,
    Resumed,
}

public sealed record VirtualNetworkLinkTransition(
    long ReplaySequence,
    TimeSpan Time,
    long SenderEndpointId,
    long ReceiverEndpointId,
    VirtualNetworkLinkTransitionKind Kind,
    long SenderGeneration,
    long ReceiverGeneration);

public enum VirtualNetworkTimeOperationKind
{
    AdvanceBy,
    DrainReady,
    RunUntilIdle,
}

public enum VirtualNetworkTimeOperationPhase
{
    Started,
    Completed,
    Failed,
}

public sealed record VirtualNetworkTimeOperation(
    long ReplaySequence,
    TimeSpan Time,
    VirtualNetworkTimeOperationKind Kind,
    VirtualNetworkTimeOperationPhase Phase,
    TimeSpan Elapsed);

public enum VirtualNetworkDeliveryOperationKind
{
    Started,
    Completed,
    Failed,
}

public sealed record VirtualNetworkDeliveryOperation(
    long ReplaySequence,
    TimeSpan Time,
    VirtualNetworkDeliveryOperationKind Kind,
    long DeliverySequence);

public sealed class VirtualNetworkReplay
{
    public IReadOnlyList<VirtualNetworkScheduleInput> SchedulingInputs { get; }
    public IReadOnlyList<VirtualNetworkConnectionTransition> ConnectionTransitions { get; }
    public IReadOnlyList<VirtualNetworkLinkTransition> LinkTransitions { get; }
    public IReadOnlyList<VirtualNetworkTimeOperation> TimeOperations { get; }
    public IReadOnlyList<VirtualNetworkDeliveryOperation> DeliveryOperations { get; }

    public VirtualNetworkReplay(
        IReadOnlyList<VirtualNetworkScheduleInput> schedulingInputs,
        IReadOnlyList<VirtualNetworkConnectionTransition> connectionTransitions,
        IReadOnlyList<VirtualNetworkLinkTransition> linkTransitions,
        IReadOnlyList<VirtualNetworkTimeOperation> timeOperations,
        IReadOnlyList<VirtualNetworkDeliveryOperation> deliveryOperations)
    {
        if (schedulingInputs == null) throw new ArgumentNullException(nameof(schedulingInputs));
        if (connectionTransitions == null) throw new ArgumentNullException(nameof(connectionTransitions));
        if (linkTransitions == null) throw new ArgumentNullException(nameof(linkTransitions));
        if (timeOperations == null) throw new ArgumentNullException(nameof(timeOperations));
        if (deliveryOperations == null) throw new ArgumentNullException(nameof(deliveryOperations));
        SchedulingInputs = schedulingInputs;
        ConnectionTransitions = connectionTransitions;
        LinkTransitions = linkTransitions;
        TimeOperations = timeOperations;
        DeliveryOperations = deliveryOperations;
    }
}

public sealed class VirtualNetworkStateSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; } = CurrentSchemaVersion;
    public long CurrentTimeTicks { get; }
    public long DefaultLatencyTicks { get; }
    public int PendingDeliveryLimit { get; }
    public int PendingDeliveryHighWaterMark { get; }
    public int BackpressureCount { get; }
    public IReadOnlyList<VirtualNetworkEndpointState> Endpoints { get; }
    public IReadOnlyList<VirtualNetworkLinkLatencyState> LinkLatencies { get; }
    public IReadOnlyList<VirtualNetworkPausedLinkState> PausedLinks { get; }
    public IReadOnlyList<VirtualNetworkPendingDeliveryState> PendingDeliveries { get; }
    public string StateDigest { get; }

    public VirtualNetworkStateSnapshot(
        long currentTimeTicks,
        long defaultLatencyTicks,
        int pendingDeliveryLimit,
        int pendingDeliveryHighWaterMark,
        int backpressureCount,
        IReadOnlyList<VirtualNetworkEndpointState> endpoints,
        IReadOnlyList<VirtualNetworkLinkLatencyState> linkLatencies,
        IReadOnlyList<VirtualNetworkPausedLinkState> pausedLinks,
        IReadOnlyList<VirtualNetworkPendingDeliveryState> pendingDeliveries,
        string stateDigest)
    {
        CurrentTimeTicks = currentTimeTicks;
        DefaultLatencyTicks = defaultLatencyTicks;
        PendingDeliveryLimit = pendingDeliveryLimit;
        PendingDeliveryHighWaterMark = pendingDeliveryHighWaterMark;
        BackpressureCount = backpressureCount;
        Endpoints = endpoints;
        LinkLatencies = linkLatencies;
        PausedLinks = pausedLinks;
        PendingDeliveries = pendingDeliveries;
        StateDigest = stateDigest;
    }
}

public sealed record VirtualNetworkEndpointState(
    long EndpointId,
    long Generation,
    bool IsConnected);

public sealed record VirtualNetworkLinkLatencyState(
    long SenderEndpointId,
    long ReceiverEndpointId,
    long LatencyTicks);

public sealed record VirtualNetworkPausedLinkState(
    long SenderEndpointId,
    long ReceiverEndpointId);

public sealed record VirtualNetworkPendingDeliveryState(
    long DeliverySequence,
    long SenderEndpointId,
    long ReceiverEndpointId,
    long SenderGeneration,
    long ReceiverGeneration,
    string Channel,
    long DueTimeTicks);

public enum VirtualNetworkTraceKind
{
    Scheduled,
    Delivered,
    Canceled,
    Superseded,
    DeliveryFailed,
    Backpressure,
    LinkPaused,
    LinkResumed,
    Disconnected,
    Reconnected,
    StaleConnectionRejected,
    DisconnectedSendRejected,
}

public sealed record VirtualNetworkTraceEntry(
    long Sequence,
    TimeSpan Time,
    VirtualNetworkTraceKind Kind,
    long? SenderEndpointId,
    long? ReceiverEndpointId,
    string? Channel,
    long? DeliverySequence,
    long? SenderGeneration,
    long? ReceiverGeneration,
    int PendingDeliveryCount,
    string? Detail);

public sealed class VirtualNetworkBackpressureException : InvalidOperationException
{
    public int PendingDeliveryLimit { get; }

    public VirtualNetworkBackpressureException(int pendingDeliveryLimit)
        : base($"The virtual network pending-delivery limit of {pendingDeliveryLimit} was reached")
    {
        PendingDeliveryLimit = pendingDeliveryLimit;
    }
}

public sealed class VirtualNetworkStaleConnectionException : InvalidOperationException
{
    public VirtualNetworkStaleConnectionException()
        : base("The virtual network connection generation is stale") { }
}
