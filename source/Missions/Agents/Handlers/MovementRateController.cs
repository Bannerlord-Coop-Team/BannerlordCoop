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
using TaleWorlds.Engine;
using TaleWorlds.Engine.Options;

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
    public int FrameLimitHz { get; }
    public int PerformanceCeilingHz { get; }
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
        int frameLimitHz,
        int performanceCeilingHz,
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
        FrameLimitHz = frameLimitHz;
        PerformanceCeilingHz = performanceCeilingHz;
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
    int GetReceiverCapHz(string controllerId);
    bool TrySetForcedBulkHz(int? hz, out string error);
    bool TrySetForcedReceiverCapHz(int? hz, out string error);
}

/// <summary>Selects local movement production cadence from explicit policy and measured work.</summary>
public sealed class MovementRateController : IMovementRateController
{
    private static readonly ILogger Logger = LogManager.GetLogger<MovementRateController>();
    private static readonly int[] RatesAscending = { 10, 15, 20, 30, 40, 60 };
    private const float ReportIntervalSeconds = 1f;
    private const float PeerCapLifetimeSeconds = 3.5f;
    private const int ReceiverCapHeartbeatMilliseconds = 1000;
    private const int HealthyWindowsBeforeIncrease = 4;
    private const int MaximumAdaptiveHz = 60;
    private const double RejectedRateRetryCostRatio = 0.9d;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IMissionContext missionContext;
    private readonly Func<long> timestampProvider;
    private readonly long timestampFrequency;
    private readonly Func<int> frameLimitProvider;
    private readonly object gate = new object();
    private readonly Dictionary<string, ReceiverCapEntry> receiverCaps =
        new Dictionary<string, ReceiverCapEntry>();
    private readonly Dictionary<int, RejectedRateState> rejectedRecoveryRates =
        new Dictionary<int, RejectedRateState>();
    private readonly System.Threading.Timer receiverCapHeartbeatTimer;

    private MovementCadenceProfile profile = MovementCadenceProfile.Location;
    private bool configured;
    private bool disposed;
    private int bulkHz = 40;
    private int priorityHz = 40;
    private int frameLimitHz = MaximumAdaptiveHz;
    private int effectiveFrameLimitHz = MaximumAdaptiveHz;
    private int performanceCeilingHz = 40;
    private int localAdaptiveHz = 40;
    private int automaticReceiverCapHz = 40;
    private int advertisedReceiverCapHz = 40;
    private int? peerReceiverCapHz;
    private string peerReceiverCapSource;
    private int? forcedBulkHz;
    private int? forcedReceiverCapHz;
    private int activeAgents;
    private int locallyControlledAgents;
    private int healthyWindows;
    private int healthyReceiverWindows;
    private int? activeRecoveryProbeRate;
    private string localReason = "location-fixed";
    private string reason = "location-fixed";
    private long receiverCapSequence;

    private float reportElapsed;
    private float frameElapsed;
    private int frameCount;
    private double sendMilliseconds;
    private double bulkSendMilliseconds;
    private double receiveApplyMilliseconds;
    private double maximumReceiverQueueMilliseconds;
    private long wireBytes;
    private int maximumDeferredSnapshots;
    private float maximumDeferredAgeSeconds;
    private int bulkPolls;
    private int priorityOnlyPolls;

    private float lastFramesPerSecond;
    private double lastSenderMillisecondsPerSecond;
    private double lastBulkSendMillisecondsPerSecond;
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

    private sealed class RejectedRateState
    {
        public int Failures;
        public bool HasLowerTierBaseline;
        public double LowerTierBulkMillisecondsPerPoll;
        public double LowerTierSenderMillisecondsPerSecond;
        public float LowerTierFramesPerSecond;
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
            ReadFrameLimitHz,
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
        Func<int> frameLimitProvider,
        bool enableHeartbeat)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (controllerIdProvider == null) throw new ArgumentNullException(nameof(controllerIdProvider));
        if (timestampProvider == null) throw new ArgumentNullException(nameof(timestampProvider));
        if (frameLimitProvider == null) throw new ArgumentNullException(nameof(frameLimitProvider));
        if (timestampFrequency <= 0) throw new ArgumentOutOfRangeException(nameof(timestampFrequency));

        this.network = network;
        this.messageBroker = messageBroker;
        this.controllerIdProvider = controllerIdProvider;
        this.missionContext = missionContext;
        this.timestampProvider = timestampProvider;
        this.timestampFrequency = timestampFrequency;
        this.frameLimitProvider = frameLimitProvider;
        UpdateFrameLimit();

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
            rejectedRecoveryRates.Clear();
            activeRecoveryProbeRate = null;
            switch (profile)
            {
                case MovementCadenceProfile.Location:
                    performanceCeilingHz = 40;
                    localAdaptiveHz = 40;
                    automaticReceiverCapHz = 40;
                    advertisedReceiverCapHz = forcedReceiverCapHz ?? automaticReceiverCapHz;
                    localReason = "location-fixed";
                    break;
                case MovementCadenceProfile.Tournament:
                    performanceCeilingHz = 60;
                    localAdaptiveHz = 60;
                    automaticReceiverCapHz = 60;
                    advertisedReceiverCapHz = forcedReceiverCapHz ?? automaticReceiverCapHz;
                    localReason = "tournament-fixed";
                    break;
                case MovementCadenceProfile.Battle:
                    int startingBattleHz = Math.Min(
                        40,
                        NormalizeRate(effectiveFrameLimitHz));
                    performanceCeilingHz = NormalizeRate(effectiveFrameLimitHz);
                    localAdaptiveHz = startingBattleHz;
                    automaticReceiverCapHz = startingBattleHz;
                    advertisedReceiverCapHz = forcedReceiverCapHz ?? automaticReceiverCapHz;
                    localReason = "battle-start";
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

            double measuredMilliseconds = Math.Max(0d, elapsedMilliseconds);
            sendMilliseconds += measuredMilliseconds;
            wireBytes += Math.Max(0L, traffic.SentBytes);
            maximumDeferredSnapshots = Math.Max(
                maximumDeferredSnapshots,
                traffic.DeferredSnapshots);
            maximumDeferredAgeSeconds = Math.Max(
                maximumDeferredAgeSeconds,
                traffic.MaximumDeferredAgeSeconds);
            if (includesAuthoritativeAgents)
            {
                bulkSendMilliseconds += measuredMilliseconds;
                bulkPolls++;
            }
            else
            {
                priorityOnlyPolls++;
            }
        }
    }

    public int GetReceiverCapHz(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return RatesAscending[RatesAscending.Length - 1];

        lock (gate)
        {
            if (disposed || profile != MovementCadenceProfile.Battle)
                return RatesAscending[RatesAscending.Length - 1];

            PruneExpiredReceiverCaps();
            return receiverCaps.TryGetValue(controllerId, out ReceiverCapEntry receiverCap)
                ? receiverCap.MaximumBulkHz
                : RatesAscending[RatesAscending.Length - 1];
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

        if (receiverCapHeartbeatTimer != null)
        {
            using (var callbackCompleted = new System.Threading.ManualResetEvent(false))
            {
                if (receiverCapHeartbeatTimer.Dispose(callbackCompleted))
                    callbackCompleted.WaitOne();
            }
        }
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
        lastBulkSendMillisecondsPerSecond = bulkSendMilliseconds / duration;
        lastReceiverApplyMillisecondsPerSecond = receiveApplyMilliseconds / duration;
        lastMaximumReceiverQueueMilliseconds = maximumReceiverQueueMilliseconds;
        lastWireBytesPerSecond = (long)Math.Round(wireBytes / duration);
        lastMaximumDeferredSnapshots = maximumDeferredSnapshots;
        lastMaximumDeferredAgeSeconds = maximumDeferredAgeSeconds;
        lastBulkPollsPerSecond = (int)Math.Round(bulkPolls / duration);
        lastPriorityOnlyPollsPerSecond = (int)Math.Round(priorityOnlyPolls / duration);
        UpdateFrameLimit();

        int desiredReceiverCapHz = CalculateReceiverCap(
            lastFramesPerSecond,
            lastReceiverApplyMillisecondsPerSecond,
            lastMaximumReceiverQueueMilliseconds,
            effectiveFrameLimitHz);
        EvaluateReceiverCap(desiredReceiverCapHz);
        advertisedReceiverCapHz = forcedReceiverCapHz ?? automaticReceiverCapHz;

        if (profile == MovementCadenceProfile.Battle)
            EvaluateBattleRate(desiredReceiverCapHz);
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

    private void EvaluateBattleRate(int receiverCeilingHz)
    {
        int senderCeilingHz = CalculateSenderCeiling(
            lastFramesPerSecond,
            lastSenderMillisecondsPerSecond,
            effectiveFrameLimitHz);
        performanceCeilingHz = Math.Min(senderCeilingHz, receiverCeilingHz);
        int desired = performanceCeilingHz;
        CapturePendingLowerTierBaseline();
        if (desired < localAdaptiveHz)
        {
            if (senderCeilingHz < localAdaptiveHz)
                RememberRejectedRate(localAdaptiveHz);
            else
                ForgetActiveRecoveryProbe(localAdaptiveHz);
            localAdaptiveHz = desired;
            healthyWindows = 0;
            localReason = "battle-performance";
            return;
        }

        if (activeRecoveryProbeRate == localAdaptiveHz)
        {
            rejectedRecoveryRates.Remove(localAdaptiveHz);
            activeRecoveryProbeRate = null;
        }

        if (desired <= localAdaptiveHz)
        {
            healthyWindows = 0;
            if (desired == localAdaptiveHz && desired <= 40)
                localReason = "battle-performance";
            return;
        }

        int candidate = Math.Min(desired, NextHigherRate(localAdaptiveHz));
        bool retryingRejectedRate = rejectedRecoveryRates.ContainsKey(candidate);
        if (retryingRejectedRate &&
            !CanRetryRejectedRate(candidate))
        {
            healthyWindows = 0;
            return;
        }

        // Probe one tier at a time so each higher cadence proves its own cost before another increase.
        healthyWindows++;
        if (healthyWindows < HealthyWindowsBeforeIncrease) return;

        localAdaptiveHz = candidate;
        if (retryingRejectedRate)
        {
            if (rejectedRecoveryRates[candidate].Failures > 1)
                CaptureLowerTierBaseline(rejectedRecoveryRates[candidate]);
            activeRecoveryProbeRate = candidate;
        }
        healthyWindows = 0;
        localReason = "battle-recovered";
    }

    private bool CanRetryRejectedRate(int candidate)
    {
        if (lastBulkPollsPerSecond <= 0 ||
            !rejectedRecoveryRates.TryGetValue(
                candidate,
                out RejectedRateState rejectedRate))
        {
            return false;
        }

        if (rejectedRate.Failures <= 1) return true;

        double bulkMillisecondsPerPoll =
            lastBulkSendMillisecondsPerSecond / lastBulkPollsPerSecond;
        if (!rejectedRate.HasLowerTierBaseline) return false;

        bool bulkCostImproved =
            rejectedRate.LowerTierBulkMillisecondsPerPoll > 0d &&
            bulkMillisecondsPerPoll <=
                rejectedRate.LowerTierBulkMillisecondsPerPoll * RejectedRateRetryCostRatio;
        bool senderCostImproved =
            rejectedRate.LowerTierSenderMillisecondsPerSecond > 0d &&
            lastSenderMillisecondsPerSecond <=
                rejectedRate.LowerTierSenderMillisecondsPerSecond * RejectedRateRetryCostRatio;
        int baselineFrameCeilingHz = CalculateSenderCeiling(
            rejectedRate.LowerTierFramesPerSecond,
            0d,
            effectiveFrameLimitHz);
        int currentFrameCeilingHz = CalculateSenderCeiling(
            lastFramesPerSecond,
            0d,
            effectiveFrameLimitHz);
        bool frameRateImproved =
            (baselineFrameCeilingHz < candidate && currentFrameCeilingHz >= candidate) ||
            (rejectedRate.LowerTierFramesPerSecond > 0f &&
                lastFramesPerSecond >=
                    rejectedRate.LowerTierFramesPerSecond / RejectedRateRetryCostRatio);
        if (!bulkCostImproved && !senderCostImproved && !frameRateImproved)
            return false;

        double projectedBulkMillisecondsPerSecond =
            bulkMillisecondsPerPoll * candidate;
        return CalculateSenderCeiling(
                lastFramesPerSecond,
                projectedBulkMillisecondsPerSecond,
                effectiveFrameLimitHz) >= candidate;
    }

    private void RememberRejectedRate(int rate)
    {
        if (!rejectedRecoveryRates.TryGetValue(rate, out RejectedRateState rejectedRate))
        {
            rejectedRate = new RejectedRateState();
            rejectedRecoveryRates.Add(rate, rejectedRate);
        }

        bool failedQualifiedProbe =
            activeRecoveryProbeRate == rate && rejectedRate.Failures > 1;
        rejectedRate.Failures++;
        if (!failedQualifiedProbe)
            rejectedRate.HasLowerTierBaseline = false;
        activeRecoveryProbeRate = null;
    }

    private void CapturePendingLowerTierBaseline()
    {
        int candidate = NextHigherRate(localAdaptiveHz);
        if (candidate <= localAdaptiveHz ||
            !rejectedRecoveryRates.TryGetValue(candidate, out RejectedRateState rejectedRate) ||
            rejectedRate.Failures <= 1 ||
            rejectedRate.HasLowerTierBaseline)
        {
            return;
        }

        CaptureLowerTierBaseline(rejectedRate);
    }

    private void CaptureLowerTierBaseline(RejectedRateState rejectedRate)
    {
        rejectedRate.HasLowerTierBaseline = true;
        rejectedRate.LowerTierBulkMillisecondsPerPoll = lastBulkPollsPerSecond > 0
            ? lastBulkSendMillisecondsPerSecond / lastBulkPollsPerSecond
            : 0d;
        rejectedRate.LowerTierSenderMillisecondsPerSecond =
            lastSenderMillisecondsPerSecond;
        rejectedRate.LowerTierFramesPerSecond = lastFramesPerSecond;
    }

    private void ForgetActiveRecoveryProbe(int rate)
    {
        if (activeRecoveryProbeRate != rate) return;

        rejectedRecoveryRates.Remove(rate);
        activeRecoveryProbeRate = null;
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
            bulkHz = localAdaptiveHz;
            reason = localReason;
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
            frameLimitHz,
            performanceCeilingHz,
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

    private static int CalculateSenderCeiling(
        float framesPerSecond,
        double senderMillisecondsPerSecond,
        int effectiveFrameLimitHz)
    {
        float normalizedFramesPerSecond = NormalizeFramesPerSecond(
            framesPerSecond,
            effectiveFrameLimitHz);
        double duty = senderMillisecondsPerSecond / 1000d;
        int desired;
        if (normalizedFramesPerSecond < 25f || duty > 0.30d)
            desired = 10;
        else if (normalizedFramesPerSecond < 35f || duty > 0.25d)
            desired = 15;
        else if (normalizedFramesPerSecond < 45f || duty > 0.20d)
            desired = 20;
        else if (normalizedFramesPerSecond < 55f || duty > 0.15d)
            desired = 30;
        else if (normalizedFramesPerSecond < 58f || duty > 0.12d)
            desired = 40;
        else
            desired = MaximumAdaptiveHz;

        return Math.Min(desired, NormalizeRate(effectiveFrameLimitHz));
    }

    private static int CalculateReceiverCap(
        float framesPerSecond,
        double receiveApplyMillisecondsPerSecond,
        double maximumQueueMilliseconds,
        int effectiveFrameLimitHz)
    {
        float normalizedFramesPerSecond = NormalizeFramesPerSecond(
            framesPerSecond,
            effectiveFrameLimitHz);
        double duty = receiveApplyMillisecondsPerSecond / 1000d;
        int desired;
        if (normalizedFramesPerSecond < 25f || duty > 0.12d ||
            maximumQueueMilliseconds > 150d)
            desired = 10;
        else if (normalizedFramesPerSecond < 35f || duty > 0.08d ||
            maximumQueueMilliseconds > 100d)
            desired = 15;
        else if (normalizedFramesPerSecond < 45f || duty > 0.05d ||
            maximumQueueMilliseconds > 75d)
            desired = 20;
        else if (normalizedFramesPerSecond < 55f || duty > 0.03d ||
            maximumQueueMilliseconds > 50d)
            desired = 30;
        else if (normalizedFramesPerSecond < 58f || duty > 0.02d)
            desired = 40;
        else
            desired = MaximumAdaptiveHz;

        return Math.Min(desired, NormalizeRate(effectiveFrameLimitHz));
    }

    private static int ReadFrameLimitHz()
    {
        // Headless hosts do not initialize native graphics config, so they use the adaptive maximum.
        if (EngineApplicationInterface.IConfig == null)
            return MaximumAdaptiveHz;

        return (int)Math.Round(NativeOptions.GetConfig(
            NativeOptions.NativeOptionsType.FrameLimiter));
    }

    private void UpdateFrameLimit()
    {
        frameLimitHz = Math.Max(0, frameLimitProvider());
        effectiveFrameLimitHz = NormalizeFrameLimit(frameLimitHz);
    }

    // Meeting a deliberate frame cap is healthy even when that cap is below 60 FPS.
    private static float NormalizeFramesPerSecond(
        float framesPerSecond,
        int effectiveFrameLimitHz) =>
        framesPerSecond * MaximumAdaptiveHz / effectiveFrameLimitHz;

    private static int NormalizeFrameLimit(int configuredFrameLimitHz)
    {
        if (configuredFrameLimitHz <= 0) return MaximumAdaptiveHz;
        return Math.Max(
            RatesAscending[0],
            Math.Min(MaximumAdaptiveHz, configuredFrameLimitHz));
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
        NetworkMovementReceiverCap? advertisement;
        lock (gate)
        {
            if (disposed) return;
            advertisement = CreateReceiverCapAdvertisement();
        }

        if (advertisement.HasValue)
            network.SendAll(advertisement.Value);
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
            receiverCaps.Remove(payload.What.ControllerId);
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
        bulkSendMilliseconds = 0d;
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
            "frameLimitHz={FrameLimitHz} performanceCeilingHz={PerformanceCeilingHz} " +
            "localAdaptiveHz={LocalAdaptiveHz} " +
            "receiverCapHz={ReceiverCapHz} peerCapHz={PeerCapHz} peerCapSource={PeerCapSource} " +
            "agents={ActiveAgents} localAgents={LocalAgents} controllers={Controllers} fps={Fps:0.0} " +
            "senderMsPerSecond={SenderMs:0.00} receiverApplyMsPerSecond={ReceiverMs:0.00} " +
            "receiverQueueMs={QueueMs:0.00} wireBytesPerSecond={WireBytes} deferred={Deferred} " +
            "deferredAge={DeferredAge:0.000} bulkPolls={BulkPolls} priorityOnlyPolls={PriorityPolls} reason={Reason}",
            snapshot.Profile,
            snapshot.BulkHz,
            snapshot.PriorityHz,
            snapshot.FrameLimitHz,
            snapshot.PerformanceCeilingHz,
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
