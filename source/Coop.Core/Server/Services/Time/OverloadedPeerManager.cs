using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
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

    private TimeControlEnum? originalSpeed;
    private volatile IEnumerable<NetPeer> cachedOverloadedPeers = Array.Empty<NetPeer>();

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
        IConnectionCollection connectionCollection
    )
    {
        this.config = config;
        this.messageBroker = messageBroker;
        this.network = network;
        this.timeControlInterface = timeControlInterface;
        this.connectionCollection = connectionCollection;

        // Adds pause policy to time handler
        timeControlInterface.AddUnpausePolicy(PlayersOverloadedPolicy);
    }

    public void Dispose()
    {
        // Removes pause policy from time handler
        timeControlInterface.RemoveUnpausePolicy(PlayersOverloadedPolicy);
    }

    private static int GetQueueDepth(NetPeer peer)
    {
        return peer.GetPacketsCountInReliableQueue(0, true) +
               peer.GetPacketsCountInReliableQueue(0, false);
    }

    private List<NetPeer> GetPeersAboveThreshold(int threshold)
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

    // One line per peer, e.g. "2@127.0.0.1 queue=12345 ping=87ms" — enough to tell from the log
    // alone which peer tripped the pause and how far it has drained since.
    private string DescribePeerQueues(IEnumerable<NetPeer> peers)
    {
        return string.Join(", ", peers.Select(peer =>
            $"{peer.Id}@{peer.Address} queue={GetQueueDepth(peer)} ping={peer.Ping}ms"));
    }

    public void CheckForOverloadedPeers()
    {
        // While paused for overload, hold until every peer has drained below the (lower) resume
        // threshold, not just back under the pause threshold. The gap between the two thresholds is
        // hysteresis: it stops a chronically slow peer from flapping pause/resume around one limit.
        if (originalSpeed.HasValue)
        {
            var stillDraining = GetPeersAboveThreshold(config.ResumePacketsInQueue);
            if (stillDraining.Any())
            {
                if (DateTime.UtcNow - lastPauseDepthLogUtc >= PauseDepthLogInterval)
                {
                    lastPauseDepthLogUtc = DateTime.UtcNow;
                    Logger.Information(
                        "Catch-up pause ongoing for {Seconds:0}s; peers above resume threshold ({ResumeThreshold}): {PeerQueues}",
                        (DateTime.UtcNow - pauseStartedUtc).TotalSeconds,
                        config.ResumePacketsInQueue,
                        DescribePeerQueues(stillDraining));
                }
                return;
            }

            ResumeTime();
            return;
        }

        var overloadedPeers = GetPeersAboveThreshold(config.MaxPacketsInQueue);
        if (overloadedPeers.Count == 0)
            return;

        originalSpeed = timeControlInterface.GetTimeControl();
        cachedOverloadedPeers = overloadedPeers;
        pauseStartedUtc = DateTime.UtcNow;
        lastPauseDepthLogUtc = DateTime.UtcNow;

        Logger.Information(
            "Pausing campaign time: {PeerCount} peer(s) above {PauseThreshold} queued reliable packets: {PeerQueues}",
            overloadedPeers.Count,
            config.MaxPacketsInQueue,
            DescribePeerQueues(overloadedPeers));

        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
        NotifyAll($"{overloadedPeers.Count} clients are catching up. Pausing...");
    }

    private void ResumeTime()
    {
        if (!originalSpeed.HasValue) return;

        var resumeSpeed = originalSpeed.Value;
        originalSpeed = null;
        cachedOverloadedPeers = Array.Empty<NetPeer>();   // clear first so the policy allows it

        Logger.Information(
            "Resuming campaign time after {Seconds:0.0}s catch-up pause; all peers below {ResumeThreshold} queued reliable packets",
            (DateTime.UtcNow - pauseStartedUtc).TotalSeconds,
            config.ResumePacketsInQueue);

        timeControlInterface.ServerSetTimeControl(resumeSpeed);
        NotifyAll("All clients synchronized. Resuming...");
    }

    /// <summary>
    /// Policy to prevent unpausing when a client queue is overloaded
    /// </summary>
    /// <returns>True if unpausing is allowed, otherwise false</returns>
    private bool PlayersOverloadedPolicy()
    {
        if (cachedOverloadedPeers.Any())
        {
            NotifyAll($"{cachedOverloadedPeers.Count()} clients are catching up. Unable to change time.");
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
