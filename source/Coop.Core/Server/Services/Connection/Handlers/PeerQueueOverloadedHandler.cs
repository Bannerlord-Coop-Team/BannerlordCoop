using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Server.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Connection.Handlers;

/// <summary>
/// Handles pausing the game when a peer packet queue is overloaded and resuming the game when the overloaded queue is fully processed
/// </summary>
internal class PeerQueueOverloadedHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PeerQueueOverloadedHandler>();

    public Poller Poller { get; }

    private readonly int POLL_INTERVAL = 100;

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly TimeHandler timeHandler;
    private readonly ILogger logger;
    

    private readonly List<NetPeer> overloadedPeers = new List<NetPeer>();

    private TimeControlEnum originalSpeed;

    public PeerQueueOverloadedHandler(
        IMessageBroker messageBroker,
        INetwork network,
        TimeHandler timeHandler
    )
    {
        this.messageBroker = messageBroker;
        this.network = network;

        this.timeHandler = timeHandler;

        logger = LogManager.GetLogger<PeerQueueOverloaded>();

        // creates a new poller (does not start poller)
        Poller = new(Poll, TimeSpan.FromMilliseconds(POLL_INTERVAL));

        messageBroker.Subscribe<PeerQueueOverloaded>(Handle);

        // Adds pause policy to time handler
        timeHandler.AddUnpausePolicy(PlayersOverloadedPolicy);
    }

    public void Dispose()
    {
        Poller.Stop();

        messageBroker.Unsubscribe<PeerQueueOverloaded>(Handle);

        // Removes pause policy from time handler
        timeHandler.RemoveUnpausePolicy(PlayersOverloadedPolicy);
    }

    internal void Handle(MessagePayload<PeerQueueOverloaded> payload)
    {
        lock (overloadedPeers)
        {
            if (overloadedPeers.Contains(payload.What.NetPeer))
                return;

            overloadedPeers.Add(payload.What.NetPeer);
        }

        // Store previoes time control mode for resuming
        if (timeHandler.TryGetTimeControlMode(out TimeControlEnum prevMode))
        {
            originalSpeed = prevMode;
        }
        else
        {
            originalSpeed = TimeControlEnum.Play_1x;
        }

        // pause time
        timeHandler.SetTimeMode(TimeControlEnum.Pause);

        // notify server and clients that the game is pausing
        var msg = new SendInformationMessage($"{overloadedPeers.Count} clients are catching up, pausing");
        messageBroker.Publish(this, msg);
        network.SendAll(msg);

        logger.Information("Clients overloaded, paused.");

        // start the poll task to determine when the overloaded queue becomes clear
        Poller.Start();
    }

    internal void Poll(TimeSpan _)
    {
        lock (overloadedPeers)
        {
            // removes all peers with clear queues
            overloadedPeers.RemoveAll(peer => peer.GetPacketsCountInReliableQueue(0, true)
                                            + peer.GetPacketsCountInReliableQueue(0, false) == 0);

            // continue if any queue is not empty
            if (overloadedPeers.Count > 0) return;
        }

        ResumeTime();

        Poller.Stop();
    }

    private void ResumeTime()
    {
        // set game to speed before pause
        timeHandler.SetTimeMode(originalSpeed);

        // notify server and all clients that game is resuming
        var msg = new SendInformationMessage("All clients synchronized, resuming");
        messageBroker.Publish(this, msg);
        network.SendAll(msg);

        logger.Information("Clients synchronised, resuming.");
    }

    /// <summary>
    /// Policy to prevent unpausing when a client queue is overloaded
    /// </summary>
    /// <returns>True if unpausing is allowed, otherwise false</returns>
    private bool PlayersOverloadedPolicy()
    {
        lock (overloadedPeers)
        {
            if (overloadedPeers.Count > 0)
            {
                Logger.Information($"{overloadedPeers.Count} clients are catching up, unable to change time");
                return false;
            }

            return true;
        }
    }
}
