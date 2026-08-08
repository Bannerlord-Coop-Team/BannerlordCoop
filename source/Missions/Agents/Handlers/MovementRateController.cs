using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity;
using Missions.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Missions.Agents.Handlers;

public enum MovementCadenceProfile
{
    Location,
    Battle,
    Tournament,
}

public readonly struct MovementCadence
{
    public int BulkHz { get; }
    public int PriorityHz { get; }

    public MovementCadence(int bulkHz, int priorityHz)
    {
        BulkHz = bulkHz;
        PriorityHz = priorityHz;
    }
}

public sealed class MovementRateSnapshot
{
    public MovementCadenceProfile Profile { get; }
    public int BulkHz { get; }
    public int PriorityHz { get; }
    public int LoadCeilingHz { get; }
    public int LocalAdaptiveHz { get; }
    public int AdvertisedReceiverCapHz { get; }
    public int? PeerReceiverCapHz { get; }
    public string PeerReceiverCapSource { get; }
    public int? ForcedBulkHz { get; }
    public int? ForcedReceiverCapHz { get; }
    public int ActiveAgents { get; }
    public int LocallyControlledAgents { get; }
    public int Controllers { get; }
    public float FramesPerSecond { get; }
    public double SenderMillisecondsPerSecond { get; }
    public double ReceiverApplyMillisecondsPerSecond { get; }
    public double MaximumReceiverQueueMilliseconds { get; }
    public long WireBytesPerSecond { get; }
    public int MaximumDeferredSnapshots { get; }
    public float MaximumDeferredAgeSeconds { get; }
    public int BulkPollsPerSecond { get; }
    public int PriorityOnlyPollsPerSecond { get; }
    public string Reason { get; }

    internal MovementRateSnapshot(
        MovementCadenceProfile profile,
        int bulkHz,
        int priorityHz,
        int loadCeilingHz,
        int localAdaptiveHz,
        int advertisedReceiverCapHz,
        int? peerReceiverCapHz,
        string peerReceiverCapSource,
        int? forcedBulkHz,
        int? forcedReceiverCapHz,
        int activeAgents,
        int locallyControlledAgents,
        int controllers,
        float framesPerSecond,
        double senderMillisecondsPerSecond,
        double receiverApplyMillisecondsPerSecond,
        double maximumReceiverQueueMilliseconds,
        long wireBytesPerSecond,
        int maximumDeferredSnapshots,
        float maximumDeferredAgeSeconds,
        int bulkPollsPerSecond,
        int priorityOnlyPollsPerSecond,
        string reason)
    {
        Profile = profile;
        BulkHz = bulkHz;
        PriorityHz = priorityHz;
        LoadCeilingHz = loadCeilingHz;
        LocalAdaptiveHz = localAdaptiveHz;
        AdvertisedReceiverCapHz = advertisedReceiverCapHz;
        PeerReceiverCapHz = peerReceiverCapHz;
        PeerReceiverCapSource = peerReceiverCapSource;
        ForcedBulkHz = forcedBulkHz;
        ForcedReceiverCapHz = forcedReceiverCapHz;
        ActiveAgents = activeAgents;
        LocallyControlledAgents = locallyControlledAgents;
        Controllers = controllers;
        FramesPerSecond = framesPerSecond;
        SenderMillisecondsPerSecond = senderMillisecondsPerSecond;
        ReceiverApplyMillisecondsPerSecond = receiverApplyMillisecondsPerSecond;
        MaximumReceiverQueueMilliseconds = maximumReceiverQueueMilliseconds;
        WireBytesPerSecond = wireBytesPerSecond;
        MaximumDeferredSnapshots = maximumDeferredSnapshots;
        MaximumDeferredAgeSeconds = maximumDeferredAgeSeconds;
        BulkPollsPerSecond = bulkPollsPerSecond;
        PriorityOnlyPollsPerSecond = priorityOnlyPollsPerSecond;
        Reason = reason;
    }
}

public interface IMovementRateController : IDisposable
{
    MovementRateSnapshot Snapshot { get; }
    void Configure(MovementCadenceProfile profile);
    MovementCadence AdvanceFrame(float elapsedSeconds);
    void ReportPopulation(int activeAgents, int locallyControlledAgents);
    void ReportSend(
        double elapsedMilliseconds,
        MovementTrafficFrame traffic,
        bool includesAuthoritativeAgents);
    void ReportReceive(
        double queueMilliseconds,
        double applyMilliseconds,
        int snapshots);
    bool TrySetForcedBulkHz(int? hz, out string error);
    bool TrySetForcedReceiverCapHz(int? hz, out string error);
}

/// <summary>Selects mission movement cadence from explicit policy, load, measured work, and peer limits.</summary>
public sealed class MovementRateController : IMovementRateController
{
    private static readonly ILogger Logger = LogManager.GetLogger<MovementRateController>();
    private static readonly int[] RatesAscending = { 10, 15, 20, 30, 40, 60 };
    private const float ReportIntervalSeconds = 1f;
    private const float PeerCapLifetimeSeconds = 3.5f;
    private const int ReceiverCapHeartbeatMilliseconds = 1000;
    private const int HealthyWindowsBeforeIncrease = 4;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IMissionContext missionContext;
    private readonly Func<long> timestampProvider;
    private readonly long timestampFrequency;
    private readonly object gate = new object();
    private readonly Dictionary<string, ReceiverCapEntry> receiverCaps =
        new Dictionary<string, ReceiverCapEntry>();
    private readonly System.Threading.Timer receiverCapHeartbeatTimer;

    private MovementCadenceProfile profile = MovementCadenceProfile.Location;
    private bool configured;
    private bool disposed;
    private int bulkHz = 40;
    private int priorityHz = 40;
    private int loadCeilingHz = 40;
    private int localAdaptiveHz = 40;
    private int automaticReceiverCapHz = 60;
    private int advertisedReceiverCapHz = 60;
    private int? peerReceiverCapHz;
    private string peerReceiverCapSource;
    private int? forcedBulkHz;
    private int? forcedReceiverCapHz;
    private int activeAgents;
    private int locallyControlledAgents;
    private int healthyWindows;
    private int healthyReceiverWindows;
    private string localReason = "location-fixed";
    private string reason = "location-fixed";
    private long receiverCapSequence;

    private float reportElapsed;
    private float frameElapsed;
    private int frameCount;
    private double sendMilliseconds;
    private double receiveApplyMilliseconds;
    private double maximumReceiverQueueMilliseconds;
    private long wireBytes;
    private int maximumDeferredSnapshots;
    private float maximumDeferredAgeSeconds;
    private int bulkPolls;
    private int priorityOnlyPolls;

    private float lastFramesPerSecond;
    private double lastSenderMillisecondsPerSecond;
    private double lastReceiverApplyMillisecondsPerSecond;
    private double lastMaximumReceiverQueueMilliseconds;
    private long lastWireBytesPerSecond;
    private int lastMaximumDeferredSnapshots;
    private float lastMaximumDeferredAgeSeconds;
    private int lastBulkPollsPerSecond;
    private int lastPriorityOnlyPollsPerSecond;

    private sealed class ReceiverCapEntry
    {
        public int MaximumBulkHz;
        public long Sequence;
        public long ReceivedTimestamp;
    }

    public MovementRateController(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider,
        IMissionContext missionContext)
        : this(
            network,
            messageBroker,
            controllerIdProvider,
            missionContext,
            Stopwatch.GetTimestamp,
            Stopwatch.Frequency,
            enableHeartbeat: true)
    {
    }

    internal MovementRateController(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider,
        IMissionContext missionContext,
        Func<long> timestampProvider,
        long timestampFrequency,
        bool enableHeartbeat)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (controllerIdProvider == null) throw new ArgumentNullException(nameof(controllerIdProvider));
        if (timestampProvider == null) throw new ArgumentNullException(nameof(timestampProvider));
        if (timestampFrequency <= 0) throw new ArgumentOutOfRangeException(nameof(timestampFrequency));

        this.network = network;
        this.messageBroker = messageBroker;
        this.controllerIdProvider = controllerIdProvider;
        this.missionContext = missionContext;
        this.timestampProvider = timestampProvider;
        this.timestampFrequency = timestampFrequency;

        messageBroker.Subscribe<NetworkMovementReceiverCap>(Handle_ReceiverCap);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);

        if (enableHeartbeat)
        {
            receiverCapHeartbeatTimer = new System.Threading.Timer(
                _ => TryPublishReceiverCapHeartbeat(),
                null,
                ReceiverCapHeartbeatMilliseconds,
                ReceiverCapHeartbeatMilliseconds);
        }
    }

    public MovementRateSnapshot Snapshot
    {
        get
        {
            lock (gate)
            {
                RecomputeEffectiveRate();
                return CreateSnapshot();
            }
        }
    }

    public void Configure(MovementCadenceProfile profile)
    {
        NetworkMovementReceiverCap? advertisement = null;
        lock (gate)
        {
            if (configured && this.profile != profile)
                throw new InvalidOperationException("Movement cadence is already configured for this mission.");

            configured = true;
            this.profile = profile;
            switch (profile)
            {
                case MovementCadenceProfile.Location:
                    loadCeilingHz = 40;
                    localAdaptiveHz = 40;
                    localReason = "location-fixed";
                    break;
                case MovementCadenceProfile.Tournament:
                    loadCeilingHz = 60;
                    localAdaptiveHz = 60;
                    localReason = "tournament-fixed";
                    break;
                case MovementCadenceProfile.Battle:
                    loadCeilingHz = 60;
                    localAdaptiveHz = 60;
                    localReason = "battle-low-load";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile));
            }

            RecomputeEffectiveRate();
            advertisement = CreateReceiverCapAdvertisement();
        }

        if (advertisement.HasValue)
            network.SendAll(advertisement.Value);
    }

    public MovementCadence AdvanceFrame(float elapsedSeconds)
    {
        NetworkMovementReceiverCap? advertisement = null;
        MovementRateSnapshot report = null;
        MovementCadence cadence;
        lock (gate)
        {
            if (disposed)
                return new MovementCadence(bulkHz, priorityHz);

            if (elapsedSeconds > 0f)
            {
                reportElapsed += elapsedSeconds;
                frameElapsed += elapsedSeconds;
                frameCount++;
            }
            RecomputeEffectiveRate();
            if (reportElapsed >= ReportIntervalSeconds)
            {
                EvaluateWindow();
                advertisement = CreateReceiverCapAdvertisement();
                report = CreateSnapshot();
                ResetWindow();
            }
            cadence = new MovementCadence(bulkHz, priorityHz);
        }

        if (advertisement.HasValue)
            network.SendAll(advertisement.Value);
        if (report != null)
            LogReport(report);
        return cadence;
    }

    public void ReportPopulation(int activeAgents, int locallyControlledAgents)
    {
        lock (gate)
        {
            if (disposed) return;

            this.activeAgents = Math.Max(0, activeAgents);
            this.locallyControlledAgents = Math.Max(0, locallyControlledAgents);
            if (profile != MovementCadenceProfile.Battle) return;

            int controllerCount = GetControllerCount();
            loadCeilingHz = CalculateLoadCeiling(this.activeAgents, controllerCount);
            if (localAdaptiveHz > loadCeilingHz)
            {
                localAdaptiveHz = loadCeilingHz;
                healthyWindows = 0;
                localReason = "battle-load";
            }
            RecomputeEffectiveRate();
        }
    }

    public void ReportSend(
        double elapsedMilliseconds,
        MovementTrafficFrame traffic,
        bool includesAuthoritativeAgents)
    {
        lock (gate)
        {
            if (disposed) return;

            sendMilliseconds += Math.Max(0d, elapsedMilliseconds);
            int recipientCount = missionContext?.ControllersInMission.Count ?? 0;
            wireBytes += Math.Max(0L, traffic.SentBytes) * recipientCount;
            maximumDeferredSnapshots = Math.Max(
                maximumDeferredSnapshots,
                traffic.DeferredSnapshots);
            maximumDeferredAgeSeconds = Math.Max(
                maximumDeferredAgeSeconds,
                traffic.MaximumDeferredAgeSeconds);
            if (includesAuthoritativeAgents)
                bulkPolls++;
            else
                priorityOnlyPolls++;
        }
    }

    public void ReportReceive(
        double queueMilliseconds,
        double applyMilliseconds,
        int snapshots)
    {
        if (snapshots <= 0) return;

        lock (gate)
        {
            if (disposed) return;

            maximumReceiverQueueMilliseconds = Math.Max(
                maximumReceiverQueueMilliseconds,
                Math.Max(0d, queueMilliseconds));
            receiveApplyMilliseconds += Math.Max(0d, applyMilliseconds);
        }
    }

    public bool TrySetForcedBulkHz(int? hz, out string error)
    {
        lock (gate)
        {
            if (profile != MovementCadenceProfile.Battle)
            {
                error = "Bulk-rate overrides are only available in adaptive battle missions.";
                return false;
            }
            if (hz.HasValue && !RatesAscending.Contains(hz.Value))
            {
                error = "Rate must be auto, 10, 15, 20, 30, 40, or 60 Hz.";
                return false;
            }

            forcedBulkHz = hz;
            RecomputeEffectiveRate();
            error = null;
            return true;
        }
    }

    public bool TrySetForcedReceiverCapHz(int? hz, out string error)
    {
        NetworkMovementReceiverCap? advertisement;
        lock (gate)
        {
            if (hz.HasValue && !RatesAscending.Contains(hz.Value))
            {
                error = "Receiver cap must be auto, 10, 15, 20, 30, 40, or 60 Hz.";
                return false;
            }

            forcedReceiverCapHz = hz;
            advertisedReceiverCapHz = hz ?? automaticReceiverCapHz;
            RecomputeEffectiveRate();
            advertisement = CreateReceiverCapAdvertisement();
            error = null;
        }

        if (advertisement.HasValue)
            network.SendAll(advertisement.Value);
        return true;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            receiverCaps.Clear();
        }

        receiverCapHeartbeatTimer?.Dispose();
        messageBroker.Unsubscribe<NetworkMovementReceiverCap>(Handle_ReceiverCap);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
    }

    private void EvaluateWindow()
    {
        float duration = Math.Max(frameElapsed, 0.001f);
        lastFramesPerSecond = frameCount / duration;
        lastSenderMillisecondsPerSecond = sendMilliseconds / duration;
        lastReceiverApplyMillisecondsPerSecond = receiveApplyMilliseconds / duration;
        lastMaximumReceiverQueueMilliseconds = maximumReceiverQueueMilliseconds;
        lastWireBytesPerSecond = (long)Math.Round(wireBytes / duration);
        lastMaximumDeferredSnapshots = maximumDeferredSnapshots;
        lastMaximumDeferredAgeSeconds = maximumDeferredAgeSeconds;
        lastBulkPollsPerSecond = (int)Math.Round(bulkPolls / duration);
        lastPriorityOnlyPollsPerSecond = (int)Math.Round(priorityOnlyPolls / duration);

        int desiredReceiverCapHz = CalculateReceiverCap(
            lastFramesPerSecond,
            lastReceiverApplyMillisecondsPerSecond,
            lastMaximumReceiverQueueMilliseconds);
        EvaluateReceiverCap(desiredReceiverCapHz);
        advertisedReceiverCapHz = forcedReceiverCapHz ?? automaticReceiverCapHz;

        if (profile == MovementCadenceProfile.Battle)
            EvaluateBattleRate();
        RecomputeEffectiveRate();
    }

    private void EvaluateReceiverCap(int desiredReceiverCapHz)
    {
        if (desiredReceiverCapHz < automaticReceiverCapHz)
        {
            automaticReceiverCapHz = desiredReceiverCapHz;
            healthyReceiverWindows = 0;
            return;
        }

        if (desiredReceiverCapHz == automaticReceiverCapHz)
        {
            healthyReceiverWindows = 0;
            return;
        }

        healthyReceiverWindows++;
        if (healthyReceiverWindows < HealthyWindowsBeforeIncrease) return;

        automaticReceiverCapHz = Math.Min(
            desiredReceiverCapHz,
            NextHigherRate(automaticReceiverCapHz));
        healthyReceiverWindows = 0;
    }

    private void EvaluateBattleRate()
    {
        int performanceCeiling = CalculatePerformanceCeiling(
            lastFramesPerSecond,
            lastSenderMillisecondsPerSecond + lastReceiverApplyMillisecondsPerSecond,
            lastMaximumReceiverQueueMilliseconds,
            lastMaximumDeferredSnapshots,
            lastMaximumDeferredAgeSeconds);
        int desired = Math.Min(loadCeilingHz, performanceCeiling);
        if (desired < localAdaptiveHz)
        {
            localAdaptiveHz = desired;
            healthyWindows = 0;
            localReason = performanceCeiling < loadCeilingHz
                ? "battle-performance"
                : "battle-load";
            return;
        }

        if (desired <= localAdaptiveHz)
        {
            healthyWindows = 0;
            return;
        }

        double movementDuty =
            (lastSenderMillisecondsPerSecond + lastReceiverApplyMillisecondsPerSecond) / 1000d;
        bool healthy = lastFramesPerSecond >= 55f &&
            movementDuty <= 0.03d &&
            lastMaximumReceiverQueueMilliseconds <= 50d &&
            lastMaximumDeferredSnapshots == 0 &&
            lastMaximumDeferredAgeSeconds <= 0.15f;
        healthyWindows = healthy ? healthyWindows + 1 : 0;
        if (healthyWindows < HealthyWindowsBeforeIncrease) return;

        localAdaptiveHz = Math.Min(desired, NextHigherRate(localAdaptiveHz));
        healthyWindows = 0;
        localReason = "battle-recovered";
    }

    private void RecomputeEffectiveRate()
    {
        PruneExpiredReceiverCaps();
        FindPeerReceiverCap(out peerReceiverCapHz, out peerReceiverCapSource);

        switch (profile)
        {
            case MovementCadenceProfile.Location:
                bulkHz = 40;
                priorityHz = 40;
                reason = "location-fixed";
                return;
            case MovementCadenceProfile.Tournament:
                bulkHz = 60;
                priorityHz = 60;
                reason = "tournament-fixed";
                return;
            case MovementCadenceProfile.Battle:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (forcedBulkHz.HasValue)
        {
            bulkHz = forcedBulkHz.Value;
            reason = "battle-forced";
        }
        else
        {
            bulkHz = Math.Min(localAdaptiveHz, advertisedReceiverCapHz);
            if (peerReceiverCapHz.HasValue)
                bulkHz = Math.Min(bulkHz, peerReceiverCapHz.Value);

            if (bulkHz < localAdaptiveHz)
            {
                reason = bulkHz == advertisedReceiverCapHz
                    ? "battle-local-receiver-cap"
                    : $"battle-peer-receiver-cap:{peerReceiverCapSource}";
            }
            else
            {
                reason = localReason;
            }
        }

        priorityHz = Math.Max(40, bulkHz);
    }

    private void PruneExpiredReceiverCaps()
    {
        long now = timestampProvider();
        long lifetimeTicks = (long)(PeerCapLifetimeSeconds * timestampFrequency);
        List<string> expired = null;
        foreach (var pair in receiverCaps)
        {
            if (now - pair.Value.ReceivedTimestamp <= lifetimeTicks) continue;

            expired ??= new List<string>();
            expired.Add(pair.Key);
        }
        if (expired == null) return;

        foreach (string controllerId in expired)
            receiverCaps.Remove(controllerId);
    }

    private void FindPeerReceiverCap(out int? cap, out string source)
    {
        cap = null;
        source = null;
        foreach (var pair in receiverCaps)
        {
            if (!cap.HasValue || pair.Value.MaximumBulkHz < cap.Value)
            {
                cap = pair.Value.MaximumBulkHz;
                source = pair.Key;
            }
        }
    }

    private MovementRateSnapshot CreateSnapshot()
    {
        return new MovementRateSnapshot(
            profile,
            bulkHz,
            priorityHz,
            loadCeilingHz,
            localAdaptiveHz,
            advertisedReceiverCapHz,
            peerReceiverCapHz,
            peerReceiverCapSource,
            forcedBulkHz,
            forcedReceiverCapHz,
            activeAgents,
            locallyControlledAgents,
            GetControllerCount(),
            lastFramesPerSecond,
            lastSenderMillisecondsPerSecond,
            lastReceiverApplyMillisecondsPerSecond,
            lastMaximumReceiverQueueMilliseconds,
            lastWireBytesPerSecond,
            lastMaximumDeferredSnapshots,
            lastMaximumDeferredAgeSeconds,
            lastBulkPollsPerSecond,
            lastPriorityOnlyPollsPerSecond,
            reason);
    }

    private int GetControllerCount() =>
        1 + (missionContext?.ControllersInMission.Count ?? 0);

    internal static int CalculateLoadCeiling(int activeAgents, int controllerCount)
    {
        int loadScore = Math.Max(0, activeAgents) +
            ((Math.Max(1, controllerCount) - 1) * 25);
        if (loadScore <= 50) return 60;
        if (loadScore <= 250) return 40;
        if (loadScore <= 500) return 30;
        if (loadScore <= 900) return 20;
        if (loadScore <= 1400) return 15;
        return 10;
    }

    private static int CalculatePerformanceCeiling(
        float framesPerSecond,
        double movementMillisecondsPerSecond,
        double maximumQueueMilliseconds,
        int deferredSnapshots,
        float maximumDeferredAgeSeconds)
    {
        double duty = movementMillisecondsPerSecond / 1000d;
        if (framesPerSecond < 25f || duty > 0.12d ||
            maximumQueueMilliseconds > 150d || maximumDeferredAgeSeconds > 0.5f)
            return 10;
        if (framesPerSecond < 35f || duty > 0.08d ||
            maximumQueueMilliseconds > 100d || maximumDeferredAgeSeconds > 0.35f)
            return 15;
        if (framesPerSecond < 45f || duty > 0.05d ||
            maximumQueueMilliseconds > 75d || maximumDeferredAgeSeconds > 0.25f)
            return 20;
        if (framesPerSecond < 55f || duty > 0.03d ||
            maximumQueueMilliseconds > 50d || maximumDeferredAgeSeconds > 0.15f ||
            deferredSnapshots > 0)
            return 30;
        if (framesPerSecond < 58f || duty > 0.02d)
            return 40;
        return 60;
    }

    private static int CalculateReceiverCap(
        float framesPerSecond,
        double receiveApplyMillisecondsPerSecond,
        double maximumQueueMilliseconds)
    {
        double duty = receiveApplyMillisecondsPerSecond / 1000d;
        if (framesPerSecond < 25f || duty > 0.12d || maximumQueueMilliseconds > 150d)
            return 10;
        if (framesPerSecond < 35f || duty > 0.08d || maximumQueueMilliseconds > 100d)
            return 15;
        if (framesPerSecond < 45f || duty > 0.05d || maximumQueueMilliseconds > 75d)
            return 20;
        if (framesPerSecond < 55f || duty > 0.03d || maximumQueueMilliseconds > 50d)
            return 30;
        if (framesPerSecond < 58f || duty > 0.02d)
            return 40;
        return 60;
    }

    private static int NextHigherRate(int current)
    {
        foreach (int rate in RatesAscending)
        {
            if (rate > current) return rate;
        }
        return RatesAscending[RatesAscending.Length - 1];
    }

    private static int NormalizeRate(int requested)
    {
        int normalized = RatesAscending[0];
        foreach (int rate in RatesAscending)
        {
            if (rate > requested) break;
            normalized = rate;
        }
        return normalized;
    }

    private NetworkMovementReceiverCap? CreateReceiverCapAdvertisement()
    {
        if (!configured ||
            (profile != MovementCadenceProfile.Battle &&
                profile != MovementCadenceProfile.Tournament))
        {
            return null;
        }

        receiverCapSequence++;
        return new NetworkMovementReceiverCap(
            controllerIdProvider.ControllerId,
            advertisedReceiverCapHz,
            receiverCapSequence);
    }

    internal void PublishReceiverCapHeartbeat()
    {
        lock (gate)
        {
            if (disposed) return;

            NetworkMovementReceiverCap? advertisement = CreateReceiverCapAdvertisement();
            if (advertisement.HasValue)
                network.SendAll(advertisement.Value);
        }
    }

    private void TryPublishReceiverCapHeartbeat()
    {
        try
        {
            PublishReceiverCapHeartbeat();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to publish the movement receiver-cap heartbeat");
        }
    }

    private void Handle_ReceiverCap(MessagePayload<NetworkMovementReceiverCap> payload)
    {
        NetworkMovementReceiverCap message = payload.What;
        if (string.IsNullOrEmpty(message.ControllerId) ||
            message.ControllerId == controllerIdProvider.ControllerId)
            return;

        lock (gate)
        {
            if (disposed) return;
            if (receiverCaps.TryGetValue(message.ControllerId, out ReceiverCapEntry existing) &&
                message.Sequence <= existing.Sequence)
                return;

            receiverCaps[message.ControllerId] = new ReceiverCapEntry
            {
                MaximumBulkHz = NormalizeRate(message.MaximumBulkHz),
                Sequence = message.Sequence,
                ReceivedTimestamp = timestampProvider(),
            };
            RecomputeEffectiveRate();
        }
    }

    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        NetworkMovementReceiverCap? advertisement;
        lock (gate)
        {
            if (disposed) return;
            advertisement = CreateReceiverCapAdvertisement();
        }

        if (advertisement.HasValue)
            network.Send(payload.What.ControllerId, advertisement.Value);
    }

    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload) =>
        RemoveReceiverCap(payload.What.ControllerId);

    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload) =>
        RemoveReceiverCap(payload.What.ControllerId);

    private void RemoveReceiverCap(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        lock (gate)
        {
            receiverCaps.Remove(controllerId);
            RecomputeEffectiveRate();
        }
    }

    private void ResetWindow()
    {
        reportElapsed %= ReportIntervalSeconds;
        frameElapsed = 0f;
        frameCount = 0;
        sendMilliseconds = 0d;
        receiveApplyMilliseconds = 0d;
        maximumReceiverQueueMilliseconds = 0d;
        wireBytes = 0;
        maximumDeferredSnapshots = 0;
        maximumDeferredAgeSeconds = 0f;
        bulkPolls = 0;
        priorityOnlyPolls = 0;
    }

    private static void LogReport(MovementRateSnapshot snapshot)
    {
        Logger.Information(
            "[MovementRate] profile={Profile} bulkHz={BulkHz} priorityHz={PriorityHz} " +
            "loadCeilingHz={LoadCeilingHz} localAdaptiveHz={LocalAdaptiveHz} " +
            "receiverCapHz={ReceiverCapHz} peerCapHz={PeerCapHz} peerCapSource={PeerCapSource} " +
            "agents={ActiveAgents} localAgents={LocalAgents} controllers={Controllers} fps={Fps:0.0} " +
            "senderMsPerSecond={SenderMs:0.00} receiverApplyMsPerSecond={ReceiverMs:0.00} " +
            "receiverQueueMs={QueueMs:0.00} wireBytesPerSecond={WireBytes} deferred={Deferred} " +
            "deferredAge={DeferredAge:0.000} bulkPolls={BulkPolls} priorityOnlyPolls={PriorityPolls} reason={Reason}",
            snapshot.Profile,
            snapshot.BulkHz,
            snapshot.PriorityHz,
            snapshot.LoadCeilingHz,
            snapshot.LocalAdaptiveHz,
            snapshot.AdvertisedReceiverCapHz,
            snapshot.PeerReceiverCapHz,
            snapshot.PeerReceiverCapSource,
            snapshot.ActiveAgents,
            snapshot.LocallyControlledAgents,
            snapshot.Controllers,
            snapshot.FramesPerSecond,
            snapshot.SenderMillisecondsPerSecond,
            snapshot.ReceiverApplyMillisecondsPerSecond,
            snapshot.MaximumReceiverQueueMilliseconds,
            snapshot.WireBytesPerSecond,
            snapshot.MaximumDeferredSnapshots,
            snapshot.MaximumDeferredAgeSeconds,
            snapshot.BulkPollsPerSecond,
            snapshot.PriorityOnlyPollsPerSecond,
            snapshot.Reason);
    }
}
