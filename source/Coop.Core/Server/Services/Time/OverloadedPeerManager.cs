using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Services.Time;

public interface IOverloadedPeerManager : IDisposable
{
    void CheckForOverloadedPeers();
}

internal class OverloadedPeerManager : IOverloadedPeerManager
{
    private static readonly ILogger Logger = LogManager.GetLogger<OverloadedPeerManager>();
    private readonly INetworkConfig config;
    private readonly IMessageBroker messageBroker;
    // Lazy breaks the construction cycle: CoopServer (the INetwork) depends on this manager, and the
    // manager only needs INetwork later, at notify time, to broadcast catch-up messages.
    private readonly Lazy<INetwork> network;
    private readonly ITimeControlInterface timeControlInterface;
    private readonly IConnectionCollection connectionCollection;
    private readonly IConnectionMessageQueue connectionMessageQueue;

    private readonly IPeerTerminator peerTerminator;

    private IAutomaticPauseLease automaticPauseLease;
    private volatile NetPeer[] cachedOverloadedPeers = Array.Empty<NetPeer>();

    // Diagnostics for the catch-up pause: when it started and when depths were last logged, so a
    // long pause leaves periodic evidence in the log instead of only an in-game message.
    private DateTime pauseStartedUtc;
    private DateTime lastPauseDepthLogUtc;
    private static readonly TimeSpan PauseDepthLogInterval = TimeSpan.FromSeconds(5);

    public OverloadedPeerManager(
        INetworkConfig config,
        IMessageBroker messageBroker,
        Lazy<INetwork> network,
        ITimeControlInterface timeControlInterface,
        IConnectionCollection connectionCollection,
        IConnectionMessageQueue connectionMessageQueue,
        IPeerTerminator peerTerminator
    )
    {
        this.config = config;
        this.messageBroker = messageBroker;
        this.network = network;
        this.timeControlInterface = timeControlInterface;
        this.connectionCollection = connectionCollection;
        this.connectionMessageQueue = connectionMessageQueue;
        this.peerTerminator = peerTerminator;

        timeControlInterface.AddUnpausePolicy(PlayersOverloadedPolicy);
    }

    public void Dispose()
    {
        timeControlInterface.RemoveUnpausePolicy(PlayersOverloadedPolicy);
    }

    private static int GetQueueDepth(NetPeer peer)
    {
        return peer.GetPacketsCountInReliableQueue(0, true) +
               peer.GetPacketsCountInReliableQueue(0, false);
    }

    private NetPeer[] GetPeersAboveThreshold(ICollection<IConnectionLogic> connections, int threshold)
    {
        List<NetPeer>? overloadedPeers = null;
        foreach (var connection in connections)
        {
            var state = connection.State;
            var peer = connection.Peer;
            if (state?.IsLoading == false && GetQueueDepth(peer) > threshold)
            {
                (overloadedPeers ??= new()).Add(peer);
            }
            // Pre-entry backlog can pause the campaign. Save transfer and replay have their own limits.
            else if (state is LoadingState { IsPreEntryPending: true } &&
                     connectionMessageQueue.TryGetCatchUpPacketsRemaining(peer, out int backlog) &&
                     backlog > threshold)
            {
                (overloadedPeers ??= new()).Add(peer);
            }
        }

        return overloadedPeers?.ToArray() ?? Array.Empty<NetPeer>();
    }

    private void AbortStalledJoin(LoadingState state, DateTime utcNow)
    {
        var peer = state.ConnectionLogic.Peer;
        TimeSpan elapsed = state.ObserveJoinStall(utcNow);
        int queueDepth = GetReportedQueueDepth(peer);
        connectionMessageQueue.TryGetCatchUpPendingBytes(peer, out long pendingBytes);
        bool timedOut = elapsed >= NetworkJoinLimits.ReplayAppliedTimeout;
        bool queueExceeded = queueDepth > NetworkJoinLimits.MaxReplayPackets ||
                             connectionMessageQueue.HasCatchUpOverflowed(peer);
        bool bytesExceeded = pendingBytes > NetworkJoinLimits.MaxReplayPendingBytes;
        // The capped join ignores further baseline requests, so nothing can make it progress again
        bool baselinesExhausted = state.HasExhaustedBaselines;
        if (!timedOut && !queueExceeded && !bytesExceeded && !baselinesExhausted) return;

        // Network receive follows this check, so make the join terminal before a late acknowledgement can run.
        if (!state.TryAbortJoinCatchUp()) return;

        string queueDescription = connectionMessageQueue.DescribeCatchUp(peer);
        int clearedPackets = connectionMessageQueue.AbortCatchUp(peer);
        string disconnectCode = baselinesExhausted
            ? DisconnectReasons.JoinBaselineLimit
            : timedOut
                ? DisconnectReasons.JoinReplayAppliedTimeout
                : DisconnectReasons.JoinReplayQueueLimit;

        Logger.Warning(
            "Aborting stalled campaign join for peer {Peer}: reason={Reason} phase={Phase} " +
            "stalled={ElapsedSeconds:0.0}s timeout={TimeoutSeconds:0.0}s " +
            "queue={QueueDepth} queueLimit={QueueLimit} pendingBytes={PendingBytes} " +
            "pendingByteLimit={PendingByteLimit} cleared={ClearedPackets} {QueueDescription}",
            peer.Id,
            disconnectCode,
            state.JoinPhaseName,
            elapsed.TotalSeconds,
            NetworkJoinLimits.ReplayAppliedTimeout.TotalSeconds,
            queueDepth,
            NetworkJoinLimits.MaxReplayPackets,
            pendingBytes,
            NetworkJoinLimits.MaxReplayPendingBytes,
            clearedPackets,
            queueDescription);

        peerTerminator.Disconnect(peer, disconnectCode);
    }

    private void AbortStalledPreEntryJoin(LoadingState state, DateTime utcNow)
    {
        var peer = state.ConnectionLogic.Peer;
        if (!connectionMessageQueue.TryGetCatchUpPacketsRemaining(peer, out int queueDepth))
        {
            state.ClearPreEntryBackpressure();
            return;
        }

        connectionMessageQueue.TryGetCatchUpPendingBytes(peer, out long pendingBytes);
        bool queueExceeded = queueDepth > NetworkJoinLimits.MaxReplayPackets ||
                             connectionMessageQueue.HasCatchUpOverflowed(peer);
        bool bytesExceeded = pendingBytes > NetworkJoinLimits.MaxReplayPendingBytes;
        bool safetyLimitExceeded = queueExceeded || bytesExceeded;

        // Keep one watchdog window while the join queue remains above the lower threshold.
        if (!safetyLimitExceeded && queueDepth <= config.ResumePacketsInQueue)
        {
            state.ClearPreEntryBackpressure();
            return;
        }

        TimeSpan? elapsed = state.GetPreEntryBackpressureElapsed(utcNow);
        if (!safetyLimitExceeded && elapsed == null)
        {
            // While paused, a peer above the resume threshold holds the pause, so it needs the deadline too.
            if (queueDepth > config.MaxPacketsInQueue || automaticPauseLease != null)
                state.StartPreEntryBackpressure(utcNow);
            return;
        }

        if (!safetyLimitExceeded && elapsed < NetworkJoinLimits.CampaignEntryTimeout) return;

        if (!state.TryAbortPreEntryJoin()) return;

        string queueDescription = connectionMessageQueue.DescribeCatchUp(peer);
        int clearedPackets = connectionMessageQueue.AbortCatchUp(peer);
        string disconnectCode = safetyLimitExceeded
            ? DisconnectReasons.JoinReplayQueueLimit
            : DisconnectReasons.JoinCampaignEntryTimeout;

        Logger.Warning(
            "Aborting stalled pre-entry campaign join for peer {Peer}: reason={Reason} phase={Phase} " +
            "elapsed={ElapsedSeconds:0.0}s timeout={TimeoutSeconds:0.0}s " +
            "queue={QueueDepth} queueLimit={QueueLimit} pendingBytes={PendingBytes} " +
            "pendingByteLimit={PendingByteLimit} cleared={ClearedPackets} {QueueDescription}",
            peer.Id,
            disconnectCode,
            state.JoinPhaseName,
            elapsed.GetValueOrDefault().TotalSeconds,
            NetworkJoinLimits.CampaignEntryTimeout.TotalSeconds,
            queueDepth,
            NetworkJoinLimits.MaxReplayPackets,
            pendingBytes,
            NetworkJoinLimits.MaxReplayPendingBytes,
            clearedPackets,
            queueDescription);

        peerTerminator.Disconnect(peer, disconnectCode);
    }

    private void AbortStalledJoins(ICollection<IConnectionLogic> connections, DateTime utcNow)
    {
        bool hasLoadingPeers = false;
        foreach (var connection in connections)
        {
            if (connection.State is not LoadingState state) continue;
            hasLoadingPeers = true;
            if (state.IsPreEntryPending) AbortStalledPreEntryJoin(state, utcNow);
        }

        if (!hasLoadingPeers) return;

        // A peer can enter replay on the game thread during the pre-entry pass. Read its phase again.
        foreach (var connection in connections)
        {
            if (connection.State is LoadingState { IsJoinCatchUpPending: true } state)
                AbortStalledJoin(state, utcNow);
        }
    }

    private int GetReportedQueueDepth(NetPeer peer) =>
        connectionMessageQueue.TryGetCatchUpPacketsRemaining(peer, out int packetsRemaining)
            ? packetsRemaining
            : GetQueueDepth(peer);

    // One line per peer, e.g. "2@127.0.0.1 queue=12345 ping=87ms" — enough to tell from the log
    // alone which peer tripped the pause and how far it has drained since.
    private string DescribePeerQueues(IEnumerable<NetPeer> peers)
    {
        return string.Join(", ", peers.Select(peer =>
            $"{peer.Id}@{peer.Address} queue={GetReportedQueueDepth(peer)} ping={peer.Ping}ms"));
    }

    public void CheckForOverloadedPeers() => CheckForOverloadedPeers(DateTime.UtcNow);

    internal void CheckForOverloadedPeers(DateTime utcNow)
    {
        var connections = connectionCollection.GetSnapshot();
        AbortStalledJoins(connections, utcNow);

        // While paused for overload, hold until every peer has drained below the (lower) resume
        // threshold, not just back under the pause threshold. The gap between the two thresholds is
        // hysteresis: it stops a chronically slow peer from flapping pause/resume around one limit.
        int threshold = automaticPauseLease != null ? config.ResumePacketsInQueue : config.MaxPacketsInQueue;
        var overloadedPeers = GetPeersAboveThreshold(connections, threshold);
        if (automaticPauseLease != null)
        {
            cachedOverloadedPeers = overloadedPeers;
            if (overloadedPeers.Length != 0)
            {
                if (utcNow - lastPauseDepthLogUtc >= PauseDepthLogInterval)
                {
                    lastPauseDepthLogUtc = utcNow;
                    Logger.Information(
                        "Catch-up pause ongoing for {Seconds:0}s: {PeerQueues}",
                        (utcNow - pauseStartedUtc).TotalSeconds,
                        DescribePeerQueues(overloadedPeers));
                }
                return;
            }

            ResumeTime(utcNow);
            return;
        }

        if (overloadedPeers.Length == 0) return;

        PauseTime(
            overloadedPeers,
            utcNow,
            $"{overloadedPeers.Length} clients are catching up. Pausing...");
    }

    private void PauseTime(NetPeer[] overloadedPeers, DateTime utcNow, string notification)
    {
        cachedOverloadedPeers = overloadedPeers;
        pauseStartedUtc = utcNow;
        lastPauseDepthLogUtc = utcNow;

        automaticPauseLease = timeControlInterface.ServerAcquireAutomaticPause();

        Logger.Information(
            "Pausing campaign time for {PeerCount} peer(s): {PeerQueues}",
            overloadedPeers.Length,
            DescribePeerQueues(overloadedPeers));

        NotifyAll(notification);
    }

    private void ResumeTime(DateTime utcNow)
    {
        if (automaticPauseLease == null) return;

        cachedOverloadedPeers = Array.Empty<NetPeer>();   // clear first so the policy allows it

        if (!automaticPauseLease.TryRelease())
            return;

        automaticPauseLease = null;
        var currentMode = timeControlInterface.GetTimeControl();
        if (currentMode != TimeControlEnum.Pause)
        {
            Logger.Information(
                "Resuming campaign time after {Seconds:0.0}s catch-up pause: mode={Mode}",
                (utcNow - pauseStartedUtc).TotalSeconds,
                currentMode);
            NotifyAll("All clients synchronized. Resuming...");
            return;
        }

        Logger.Information(
            "Catch-up pause ended after {Seconds:0.0}s; campaign time remains paused",
            (utcNow - pauseStartedUtc).TotalSeconds);
        NotifyAll("All clients synchronized.");
    }

    /// <summary>
    /// Policy to prevent unpausing when a client queue is overloaded
    /// </summary>
    /// <returns>True if unpausing is allowed, otherwise false</returns>
    private bool PlayersOverloadedPolicy()
    {
        if (cachedOverloadedPeers.Any())
        {
            NotifyAll($"{cachedOverloadedPeers.Length} clients are catching up. Unable to change time.");
            return false;
        }

        return true;
    }

    private void NotifyAll(string message)
    {
        // notify server and clients
        var msg = new SendInformationMessage(message);
        messageBroker.Publish(this, msg);
        network.Value.SendAll(msg);
    }
}
