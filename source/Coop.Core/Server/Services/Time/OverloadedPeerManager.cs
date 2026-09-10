using Common.Logging;
using Coop.Core.Server.Connections.Messages;
using Common.Messaging;
using Common.Network;
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
    /// <summary>Tracks the last acknowledged application progress for one joining connection.</summary>
    private sealed class JoinProgress
    {
        public long Version;
        public DateTime LastProgressUtc;
    }

    private static readonly ILogger Logger = LogManager.GetLogger<OverloadedPeerManager>();
    private readonly INetworkConfig config;
    private readonly IMessageBroker messageBroker;
    // Lazy breaks the construction cycle: CoopServer (the INetwork) depends on this manager, and the
    // manager only needs INetwork later, at notify time, to broadcast catch-up messages.
    private readonly Lazy<INetwork> network;
    private readonly ITimeControlInterface timeControlInterface;
    private readonly IConnectionCollection connectionCollection;
    private readonly IConnectionMessageQueue connectionMessageQueue;

    private readonly IJoinPeerTerminator joinPeerTerminator;
    private readonly Dictionary<NetPeer, JoinProgress> joinCatchUpProgress = new(ReferenceComparer<NetPeer>.Instance);
    private readonly Dictionary<NetPeer, DateTime> preEntryBackpressureStartedUtc = new(ReferenceComparer<NetPeer>.Instance);

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
        IJoinPeerTerminator joinPeerTerminator
    )
    {
        this.config = config;
        this.messageBroker = messageBroker;
        this.network = network;
        this.timeControlInterface = timeControlInterface;
        this.connectionCollection = connectionCollection;
        this.connectionMessageQueue = connectionMessageQueue;
        this.joinPeerTerminator = joinPeerTerminator;

        timeControlInterface.AddUnpausePolicy(PlayersOverloadedPolicy);
    }

    public void Dispose()
    {
        timeControlInterface.RemoveUnpausePolicy(PlayersOverloadedPolicy);
        joinCatchUpProgress.Clear();
        preEntryBackpressureStartedUtc.Clear();
    }

    private static int GetQueueDepth(NetPeer peer)
    {
        return peer.GetPacketsCountInReliableQueue(0, true) +
               peer.GetPacketsCountInReliableQueue(0, false);
    }

    private List<NetPeer> GetLivePeersAboveThreshold(int threshold)
    {
        // Loading peers are excluded: their bulk save and held world stream are expected join traffic.
        // Once the held stream is released the connection enters CampaignState and normal overload
        // backpressure applies if its reliable world channel cannot keep up.
        return connectionCollection
            .Where(logic => logic.IsLoading == false)
            .Select(logic => logic.Peer)
            .Where(peer => GetQueueDepth(peer) > threshold)
            .ToList();
    }

    private void AbortStalledJoiners(DateTime utcNow)
    {
        var joining = connectionCollection
            .Select(logic => (logic.Peer, State: logic.State as LoadingState))
            .Where(entry => entry.State?.IsJoinCatchUpPending == true)
            .Select(entry => (entry.Peer, State: entry.State!))
            .ToArray();
        var activePeers = new HashSet<NetPeer>(
            joining.Select(entry => entry.Peer),
            ReferenceComparer<NetPeer>.Instance);

        foreach (var peer in joinCatchUpProgress.Keys
                     .Where(peer => !activePeers.Contains(peer))
                     .ToArray())
        {
            joinCatchUpProgress.Remove(peer);
        }

        foreach (var entry in joining)
        {
            long progressVersion = entry.State.JoinProgressVersion;
            if (!joinCatchUpProgress.TryGetValue(entry.Peer, out var progress))
            {
                progress = new JoinProgress
                {
                    Version = progressVersion,
                    LastProgressUtc = utcNow,
                };
                joinCatchUpProgress.Add(entry.Peer, progress);
            }
            else if (progress.Version != progressVersion)
            {
                progress.Version = progressVersion;
                progress.LastProgressUtc = utcNow;
            }

            TimeSpan elapsed = utcNow - progress.LastProgressUtc;
            int queueDepth = GetReportedQueueDepth(entry.Peer);
            connectionMessageQueue.TryGetCatchUpPendingBytes(entry.Peer, out long pendingBytes);
            bool timedOut = elapsed >= NetworkJoinLimits.ReplayAppliedTimeout;
            bool queueExceeded = queueDepth > NetworkJoinLimits.MaxReplayPackets ||
                                 connectionMessageQueue.HasCatchUpOverflowed(entry.Peer);
            bool bytesExceeded = pendingBytes > NetworkJoinLimits.MaxReplayPendingBytes;
            if (!timedOut && !queueExceeded && !bytesExceeded) continue;

            // Make the join terminal before disconnecting. Network receive is polled after this
            // check, so an acknowledgement already in flight cannot restart synchronization.
            if (!entry.State.TryAbortJoinCatchUp()) continue;

            string queueDescription = connectionMessageQueue.DescribeCatchUp(entry.Peer);
            int clearedPackets = connectionMessageQueue.AbortCatchUp(entry.Peer);
            string disconnectCode = timedOut
                ? "JoinReplayAppliedTimeout"
                : "JoinReplayQueueLimit";

            joinCatchUpProgress.Remove(entry.Peer);

            Logger.Warning(
                "Aborting stalled campaign join for peer {Peer}: reason={Reason} phase={Phase} " +
                "stalled={ElapsedSeconds:0.0}s timeout={TimeoutSeconds:0.0}s " +
                "queue={QueueDepth} queueLimit={QueueLimit} pendingBytes={PendingBytes} " +
                "pendingByteLimit={PendingByteLimit} cleared={ClearedPackets} {QueueDescription}",
                entry.Peer.Id,
                disconnectCode,
                entry.State.JoinPhaseName,
                elapsed.TotalSeconds,
                NetworkJoinLimits.ReplayAppliedTimeout.TotalSeconds,
                queueDepth,
                NetworkJoinLimits.MaxReplayPackets,
                pendingBytes,
                NetworkJoinLimits.MaxReplayPendingBytes,
                clearedPackets,
                queueDescription);

            joinPeerTerminator.Disconnect(entry.Peer, disconnectCode);
        }
    }

    private void AbortStalledPreEntryJoiners(DateTime utcNow)
    {
        var joining = connectionCollection
            .Select(logic => (logic.Peer, State: logic.State as LoadingState))
            .Where(entry => entry.State?.IsPreEntryPending == true)
            .Select(entry => (entry.Peer, State: entry.State!))
            .ToArray();
        var activePeers = new HashSet<NetPeer>(
            joining.Select(entry => entry.Peer),
            ReferenceComparer<NetPeer>.Instance);

        foreach (var peer in preEntryBackpressureStartedUtc.Keys
                     .Where(peer => !activePeers.Contains(peer))
                     .ToArray())
        {
            preEntryBackpressureStartedUtc.Remove(peer);
        }

        foreach (var entry in joining)
        {
            if (!connectionMessageQueue.TryGetCatchUpPacketsRemaining(
                    entry.Peer,
                    out int queueDepth))
            {
                preEntryBackpressureStartedUtc.Remove(entry.Peer);
                continue;
            }

            connectionMessageQueue.TryGetCatchUpPendingBytes(entry.Peer, out long pendingBytes);
            bool queueExceeded = queueDepth > NetworkJoinLimits.MaxReplayPackets ||
                                 connectionMessageQueue.HasCatchUpOverflowed(entry.Peer);
            bool bytesExceeded = pendingBytes > NetworkJoinLimits.MaxReplayPendingBytes;
            bool safetyLimitExceeded = queueExceeded || bytesExceeded;
            TimeSpan elapsed = TimeSpan.Zero;
            bool timedOut = false;

            if (!safetyLimitExceeded)
            {
                // Keep one watchdog window while the join queue remains above the lower threshold.
                if (queueDepth <= config.ResumePacketsInQueue)
                {
                    preEntryBackpressureStartedUtc.Remove(entry.Peer);
                    continue;
                }

                if (!preEntryBackpressureStartedUtc.TryGetValue(entry.Peer, out var startedUtc))
                {
                    if (queueDepth <= config.MaxPacketsInQueue) continue;

                    startedUtc = utcNow;
                    preEntryBackpressureStartedUtc.Add(entry.Peer, startedUtc);
                }

                elapsed = utcNow - startedUtc;
                timedOut = elapsed >= NetworkJoinLimits.CampaignEntryTimeout;
                if (!timedOut) continue;
            }
            else if (preEntryBackpressureStartedUtc.TryGetValue(entry.Peer, out var safetyStartedUtc))
            {
                elapsed = utcNow - safetyStartedUtc;
            }

            if (!entry.State.TryAbortPreEntryJoin()) continue;

            string queueDescription = connectionMessageQueue.DescribeCatchUp(entry.Peer);
            int clearedPackets = connectionMessageQueue.AbortCatchUp(entry.Peer);
            string disconnectCode = timedOut
                ? "JoinCampaignEntryTimeout"
                : "JoinReplayQueueLimit";

            preEntryBackpressureStartedUtc.Remove(entry.Peer);

            Logger.Warning(
                "Aborting stalled pre-entry campaign join for peer {Peer}: reason={Reason} phase={Phase} " +
                "elapsed={ElapsedSeconds:0.0}s timeout={TimeoutSeconds:0.0}s " +
                "queue={QueueDepth} queueLimit={QueueLimit} pendingBytes={PendingBytes} " +
                "pendingByteLimit={PendingByteLimit} cleared={ClearedPackets} {QueueDescription}",
                entry.Peer.Id,
                disconnectCode,
                entry.State.JoinPhaseName,
                elapsed.TotalSeconds,
                NetworkJoinLimits.CampaignEntryTimeout.TotalSeconds,
                queueDepth,
                NetworkJoinLimits.MaxReplayPackets,
                pendingBytes,
                NetworkJoinLimits.MaxReplayPendingBytes,
                clearedPackets,
                queueDescription);

            joinPeerTerminator.Disconnect(entry.Peer, disconnectCode);
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
        AbortStalledPreEntryJoiners(utcNow);
        AbortStalledJoiners(utcNow);

        // While paused for overload, hold until every peer has drained below the (lower) resume
        // threshold, not just back under the pause threshold. The gap between the two thresholds is
        // hysteresis: it stops a chronically slow peer from flapping pause/resume around one limit.
        if (automaticPauseLease != null)
        {
            var stillDraining = GetLivePeersAboveThreshold(config.ResumePacketsInQueue)
                .Distinct()
                .ToArray();
            cachedOverloadedPeers = stillDraining;
            if (stillDraining.Any())
            {
                if (utcNow - lastPauseDepthLogUtc >= PauseDepthLogInterval)
                {
                    lastPauseDepthLogUtc = utcNow;
                    Logger.Information(
                        "Catch-up pause ongoing for {Seconds:0}s: {PeerQueues}",
                        (utcNow - pauseStartedUtc).TotalSeconds,
                        DescribePeerQueues(stillDraining));
                }
                return;
            }

            ResumeTime(utcNow);
            return;
        }

        var overloadedPeers = GetLivePeersAboveThreshold(config.MaxPacketsInQueue);
        if (overloadedPeers.Count == 0) return;

        PauseTime(
            overloadedPeers.ToArray(),
            utcNow,
            $"{overloadedPeers.Count} clients are catching up. Pausing...");
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
