using Common.Logging;
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
    private static readonly ILogger Logger = LogManager.GetLogger<OverloadedPeerManager>();
    private readonly INetworkConfig config;
    private readonly IMessageBroker messageBroker;
    // Lazy breaks the construction cycle: CoopServer (the INetwork) depends on this manager, and the
    // manager only needs INetwork later, at notify time, to broadcast catch-up messages.
    private readonly Lazy<INetwork> network;
    private readonly ITimeControlInterface timeControlInterface;
    private readonly IConnectionCollection connectionCollection;
    private readonly IConnectionMessageQueue connectionMessageQueue;

    private TimeControlEnum? originalSpeed;
    private volatile NetPeer[] cachedOverloadedPeers = Array.Empty<NetPeer>();
    private readonly Dictionary<NetPeer, DateTime> joinCatchUpStartedUtc = new Dictionary<NetPeer, DateTime>();
    private readonly HashSet<NetPeer> idleJoinPeers = new HashSet<NetPeer>();
    private bool resumeAtPlayAfterIdleJoin;

    // Diagnostics for the catch-up pause: when it started and when depths were last logged, so a
    // long pause leaves periodic evidence in the log instead of only an in-game message.
    private DateTime pauseStartedUtc;
    private DateTime lastPauseDepthLogUtc;
    private static readonly TimeSpan PauseDepthLogInterval = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan JoinCatchUpPauseDelay = TimeSpan.FromSeconds(20);

    public OverloadedPeerManager(
        INetworkConfig config,
        IMessageBroker messageBroker,
        Lazy<INetwork> network,
        ITimeControlInterface timeControlInterface,
        IConnectionCollection connectionCollection,
        IConnectionMessageQueue connectionMessageQueue
    )
    {
        this.config = config;
        this.messageBroker = messageBroker;
        this.network = network;
        this.timeControlInterface = timeControlInterface;
        this.connectionCollection = connectionCollection;
        this.connectionMessageQueue = connectionMessageQueue;

        // Adds pause policy to time handler
        timeControlInterface.AddUnpausePolicy(PlayersOverloadedPolicy);
    }

    public void Dispose()
    {
        // Removes pause policy from time handler
        timeControlInterface.RemoveUnpausePolicy(PlayersOverloadedPolicy);
        joinCatchUpStartedUtc.Clear();
        idleJoinPeers.Clear();
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

    private List<NetPeer> UpdateJoinCatchUpPeers(DateTime utcNow)
    {
        var peers = connectionCollection
            .Where(logic => logic.State is LoadingState { IsJoinCatchUpPauseEligible: true })
            .Select(logic => logic.Peer)
            .ToList();
        var activePeers = new HashSet<NetPeer>(peers);

        foreach (var peer in joinCatchUpStartedUtc.Keys.Where(peer => !activePeers.Contains(peer)).ToArray())
        {
            var connection = connectionCollection.FirstOrDefault(logic => logic.Peer == peer);
            joinCatchUpStartedUtc.Remove(peer);
            if (idleJoinPeers.Remove(peer) && connection?.IsLoading == false)
            {
                resumeAtPlayAfterIdleJoin = true;
            }
        }

        foreach (var peer in peers)
        {
            if (!joinCatchUpStartedUtc.ContainsKey(peer))
            {
                joinCatchUpStartedUtc.Add(peer, utcNow);
                bool hasCampaignPeer = connectionCollection.Any(logic => logic.IsLoading == false);
                if (!hasCampaignPeer &&
                    timeControlInterface.GetTimeControl() == TimeControlEnum.Pause)
                {
                    idleJoinPeers.Add(peer);
                }
            }
        }

        return peers;
    }

    private List<NetPeer> GetStalledJoiningPeers(
        IEnumerable<NetPeer> joinCatchUpPeers,
        DateTime utcNow,
        int queueThreshold) =>
        joinCatchUpPeers
            .Where(peer => utcNow - joinCatchUpStartedUtc[peer] >= JoinCatchUpPauseDelay &&
                GetReportedQueueDepth(peer) > queueThreshold)
            .ToList();

    private int GetReportedQueueDepth(NetPeer peer) =>
        connectionMessageQueue.TryGetCatchUpPacketsRemaining(peer, out int packetsRemaining)
            ? packetsRemaining
            : GetQueueDepth(peer);

    // One line per peer, e.g. "2@127.0.0.1 queue=12345 ping=87ms" — enough to tell from the log
    // alone which peer tripped the pause and how far it has drained since.
    private string DescribePeerQueues(IEnumerable<NetPeer> peers)
    {
        return string.Join(", ", peers.Select(peer =>
        {
            string phase = connectionCollection
                .FirstOrDefault(logic => logic.Peer == peer)?.State is LoadingState loading
                ? loading.JoinPhaseName
                : "Campaign";
            return $"{peer.Id}@{peer.Address} phase={phase} " +
                   $"queue={GetReportedQueueDepth(peer)} ping={peer.Ping}ms " +
                   connectionMessageQueue.DescribeCatchUp(peer);
        }));
    }

    public void CheckForOverloadedPeers() => CheckForOverloadedPeers(DateTime.UtcNow);

    internal void CheckForOverloadedPeers(DateTime utcNow)
    {
        var joinCatchUpPeers = UpdateJoinCatchUpPeers(utcNow);
        var stalledJoiningPeers = GetStalledJoiningPeers(
            joinCatchUpPeers,
            utcNow,
            config.MaxPacketsInQueue);

        // While paused for overload, hold until every peer has drained below the (lower) resume
        // threshold, not just back under the pause threshold. The gap between the two thresholds is
        // hysteresis: it stops a chronically slow peer from flapping pause/resume around one limit.
        if (originalSpeed.HasValue)
        {
            var stillDraining = GetLivePeersAboveThreshold(config.ResumePacketsInQueue)
                .Concat(GetStalledJoiningPeers(
                    joinCatchUpPeers,
                    utcNow,
                    config.ResumePacketsInQueue))
                .Distinct()
                .ToArray();
            cachedOverloadedPeers = stillDraining;
            if (stillDraining.Any())
            {
                if (utcNow - lastPauseDepthLogUtc >= PauseDepthLogInterval)
                {
                    lastPauseDepthLogUtc = utcNow;
                    string queueDescription = DescribePeerQueues(stillDraining);
                    Logger.Information(
                        "Catch-up pause ongoing for {Seconds:0}s: {PeerQueues}",
                        (utcNow - pauseStartedUtc).TotalSeconds,
                        queueDescription);
                }
                return;
            }

            ResumeTime(utcNow);
            return;
        }

        var overloadedPeers = GetLivePeersAboveThreshold(config.MaxPacketsInQueue);
        if (stalledJoiningPeers.Count > 0)
        {
            PauseTime(
                overloadedPeers.Concat(stalledJoiningPeers).Distinct().ToArray(),
                utcNow,
                "Game paused; a joining client needs to catch up");
            return;
        }

        if (overloadedPeers.Count > 0)
        {
            PauseTime(
                overloadedPeers.ToArray(),
                utcNow,
                $"{overloadedPeers.Count} clients are catching up. Pausing...");
            return;
        }

        if (resumeAtPlayAfterIdleJoin && !originalSpeed.HasValue)
        {
            ResumeIdleServerAfterJoin();
        }
    }

    private void PauseTime(NetPeer[] overloadedPeers, DateTime utcNow, string notification)
    {
        originalSpeed = timeControlInterface.GetTimeControl();
        cachedOverloadedPeers = overloadedPeers;
        pauseStartedUtc = utcNow;
        lastPauseDepthLogUtc = utcNow;

        string queueDescription = DescribePeerQueues(overloadedPeers);
        Logger.Information(
            "Pausing campaign time for {PeerCount} peer(s): {PeerQueues}",
            overloadedPeers.Length,
            queueDescription);

        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
        NotifyAll(notification);
    }

    private void ResumeTime(DateTime utcNow)
    {
        if (!originalSpeed.HasValue) return;

        bool joinedIdleServer = resumeAtPlayAfterIdleJoin &&
            connectionCollection.Any(logic => logic.IsLoading == false);
        var resumeSpeed = joinedIdleServer
            ? TimeControlEnum.Play_1x
            : originalSpeed.Value;
        originalSpeed = null;
        resumeAtPlayAfterIdleJoin = false;
        cachedOverloadedPeers = Array.Empty<NetPeer>();   // clear first so the policy allows it

        Logger.Information(
            "Resuming campaign time after {Seconds:0.0}s catch-up pause",
            (utcNow - pauseStartedUtc).TotalSeconds);

        timeControlInterface.ServerSetTimeControl(resumeSpeed);
        NotifyAll("All clients synchronized. Resuming...");
    }

    private void ResumeIdleServerAfterJoin()
    {
        resumeAtPlayAfterIdleJoin = false;
        cachedOverloadedPeers = Array.Empty<NetPeer>();
        Logger.Information("First player finished loading; resuming idle campaign at 1x");
        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Play_1x);
        NotifyAll("All clients synchronized. Resuming...");
    }

    /// <summary>
    /// Policy to prevent unpausing when a client queue is overloaded
    /// </summary>
    /// <returns>True if unpausing is allowed, otherwise false</returns>
    private bool PlayersOverloadedPolicy()
    {
        // Policies are evaluated repeatedly while a time-control request is blocked.
        // Broadcasting here fed a new reliable message into the same joining peer's
        // gated queue on every evaluation, so the catch-up barrier could sustain its
        // own backlog indefinitely. Pause/resume notifications are emitted once by
        // PauseTime and ResumeTime instead.
        return !cachedOverloadedPeers.Any();
    }

    private void NotifyAll(string message)
    {
        // notify server and clients
        var msg = new SendInformationMessage(message);
        messageBroker.Publish(this, msg);
        network.Value.SendAll(msg);
    }
}
