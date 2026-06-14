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

public interface IOverloadedPeerManager
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
    private IEnumerable<NetPeer> cachedOverloadedPeers = Array.Empty<NetPeer>();

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

    private List<NetPeer> GetPeersAboveThreshold(int threshold)
    {
        return connectionCollection
            .Select(logic => logic.Peer)
            .Where(peer =>
            {
                var numPacketsInQueue =
                    peer.GetPacketsCountInReliableQueue(0, true) +
                    peer.GetPacketsCountInReliableQueue(0, false);

                return numPacketsInQueue > threshold;
            })
            .ToList();
    }

    public void CheckForOverloadedPeers()
    {
        // While paused for overload, hold until every peer has drained below the (lower) resume
        // threshold, not just back under the pause threshold. The gap between the two thresholds is
        // hysteresis: it stops a chronically slow peer from flapping pause/resume around one limit.
        if (originalSpeed.HasValue)
        {
            if (GetPeersAboveThreshold(config.ResumePacketsInQueue).Any())
                return;

            ResumeTime();
            return;
        }

        var overloadedPeers = GetPeersAboveThreshold(config.MaxPacketsInQueue);
        if (overloadedPeers.Count == 0)
            return;

        originalSpeed = timeControlInterface.GetTimeControl();
        cachedOverloadedPeers = overloadedPeers;

        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
        NotifyAll($"{overloadedPeers.Count} clients are catching up. Pausing...");
    }

    private void ResumeTime()
    {
        if (!originalSpeed.HasValue) return;

        var resumeSpeed = originalSpeed.Value;
        originalSpeed = null;
        cachedOverloadedPeers = Array.Empty<NetPeer>();   // clear first so the policy allows it

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
