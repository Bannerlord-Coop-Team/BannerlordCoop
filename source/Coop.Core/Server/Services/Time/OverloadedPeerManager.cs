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
    private readonly INetwork network;
    private readonly ITimeControlInterface timeControlInterface;
    private readonly IConnectionCollection connectionCollection;

    private TimeControlEnum? originalSpeed;
    private IEnumerable<NetPeer> cachedOverloadedPeers = Array.Empty<NetPeer>();

    public OverloadedPeerManager(
        INetworkConfig config,
        IMessageBroker messageBroker,
        INetwork network,
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

    private IEnumerable<NetPeer> GetOverloadedPeers()
    {
        return connectionCollection
            .Select(logic => logic.Peer)
            .Where(peer =>
            {
                var numPacketsInQueue = 
                    peer.GetPacketsCountInReliableQueue(0, true) +
                    peer.GetPacketsCountInReliableQueue(0, false);

                return config.MaxPacketsInQueue < numPacketsInQueue;
            })
            .ToList();
    }

    public void CheckForOverloadedPeers()
    {
        var overloadedPeers = GetOverloadedPeers();

        var overloadedPeerCount = overloadedPeers.Count();

        if (overloadedPeerCount <= 0)
        {
            ResumeTime();
            return;
        }

        // original speed will be cleared on resume
        if (originalSpeed.HasValue)
            return;

        originalSpeed = timeControlInterface.GetTimeControl();
        cachedOverloadedPeers = overloadedPeers;

        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
        NotifyAll($"{overloadedPeerCount} clients are catching up. Pausing...");
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
        network.SendAll(msg);
    }
}
