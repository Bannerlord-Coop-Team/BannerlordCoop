using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Connection.Handlers;

/// <summary>
/// Applies a graduated, automatic throttle to server time while one or more clients fall behind on their
/// outgoing packet queue, then lifts it once every client has caught up. Two tiers stack on top of each other:
/// <list type="bullet">
/// <item><see cref="PeerQueueCongested"/> (queue past <see cref="INetworkConfiguration.SlowDownPacketThreshold"/>)
/// caps time at <see cref="TimeControlEnum.Play_1x"/> so the client can catch up without a full stop.</item>
/// <item><see cref="PeerQueueOverloaded"/> (queue past <see cref="INetworkConfiguration.MaxPacketsInQueue"/>)
/// pauses time entirely.</item>
/// </list>
/// The speed the players had before any throttle began is captured exactly once and restored when the last
/// client catches up, so the game returns to its original speed rather than to an intermediate throttled one.
/// </summary>
internal class PeerQueueOverloadedHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PeerQueueOverloadedHandler>();

    public Poller Poller { get; }

    private readonly int POLL_INTERVAL = 100;

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ITimeControlInterface timeControlInterface;

    /// <summary>Every client currently catching up (queue past the slow-down threshold). <see cref="pausePeers"/> is a subset.</summary>
    private readonly List<NetPeer> slowPeers = new List<NetPeer>();
    /// <summary>Clients far enough behind to require a full pause (queue past the pause threshold).</summary>
    private readonly List<NetPeer> pausePeers = new List<NetPeer>();

    /// <summary>Throttle currently applied to the game, so time and messages only change on a tier transition.</summary>
    private ThrottleLevel appliedThrottle = ThrottleLevel.None;

    /// <summary>The speed the players had before throttling began; restored once every client catches up.</summary>
    private TimeControlEnum originalSpeed;

    public PeerQueueOverloadedHandler(
        IMessageBroker messageBroker,
        INetwork network,
        ITimeControlInterface timeControlInterface
    )
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.timeControlInterface = timeControlInterface;

        // creates a new poller (does not start poller)
        Poller = new(Poll, TimeSpan.FromMilliseconds(POLL_INTERVAL));

        messageBroker.Subscribe<PeerQueueOverloaded>(Handle_PeerQueueOverloaded);
        messageBroker.Subscribe<PeerQueueCongested>(Handle_PeerQueueCongested);

        // While a client is overloaded the game cannot be unpaused; while one is merely congested it cannot fast-forward.
        timeControlInterface.AddUnpausePolicy(PausePolicy);
        timeControlInterface.AddFastForwardPolicy(SlowDownPolicy);
    }

    public void Dispose()
    {
        Poller.Stop();

        messageBroker.Unsubscribe<PeerQueueOverloaded>(Handle_PeerQueueOverloaded);
        messageBroker.Unsubscribe<PeerQueueCongested>(Handle_PeerQueueCongested);

        timeControlInterface.RemoveUnpausePolicy(PausePolicy);
        timeControlInterface.RemoveFastForwardPolicy(SlowDownPolicy);
    }

    internal void Handle_PeerQueueOverloaded(MessagePayload<PeerQueueOverloaded> payload)
    {
        // An overloaded peer is also congested, so it belongs in both sets.
        Track(payload.What.NetPeer, requiresPause: true);
    }

    internal void Handle_PeerQueueCongested(MessagePayload<PeerQueueCongested> payload)
    {
        Track(payload.What.NetPeer, requiresPause: false);
    }

    private void Track(NetPeer peer, bool requiresPause)
    {
        lock (slowPeers)
        {
            // Capture the pre-throttle speed exactly once, when the first client starts catching up.
            if (slowPeers.Count == 0 && pausePeers.Count == 0)
            {
                originalSpeed = timeControlInterface.GetTimeControl();
            }

            if (slowPeers.Contains(peer) == false)
            {
                slowPeers.Add(peer);
            }

            if (requiresPause && pausePeers.Contains(peer) == false)
            {
                pausePeers.Add(peer);
            }
        }

        ApplyThrottle();

        // start the poll task to determine when the overloaded queues become clear
        Poller.Start();
    }

    internal void Poll(TimeSpan _)
    {
        lock (slowPeers)
        {
            // removes all peers with clear queues
            slowPeers.RemoveAll(IsClientCaughtUp);
            pausePeers.RemoveAll(IsClientCaughtUp);
        }

        ApplyThrottle();

        lock (slowPeers)
        {
            // stop polling once every client has caught up
            if (slowPeers.Count == 0)
            {
                Poller.Stop();
            }
        }
    }

    /// <summary>
    /// Re-evaluates the throttle the clients' queues currently warrant and, only when that level changes,
    /// sets server time accordingly and notifies everyone. Re-applying on every poll would spam the network
    /// with redundant time-control changes, so transitions are the trigger.
    /// </summary>
    private void ApplyThrottle()
    {
        ThrottleLevel level;
        TimeControlEnum target;
        int catchingUp;
        lock (slowPeers)
        {
            level = ComputeThrottle();
            if (level == appliedThrottle) return;

            appliedThrottle = level;
            target = TargetSpeed(level);
            catchingUp = slowPeers.Count;
        }

        timeControlInterface.ServerSetTimeControl(target);

        var msg = new SendInformationMessage(ThrottleMessage(level, catchingUp));
        messageBroker.Publish(this, msg);
        network.SendAll(msg);

        Logger.Information("Queue throttle now {level}, {count} clients catching up", level, catchingUp);
    }

    /// <summary>The throttle the current backlog warrants: pause beats slow-down beats none.</summary>
    private ThrottleLevel ComputeThrottle()
    {
        if (pausePeers.Count > 0) return ThrottleLevel.Pause;
        if (slowPeers.Count > 0) return ThrottleLevel.SlowTo1x;
        return ThrottleLevel.None;
    }

    /// <summary>Maps a throttle level to the speed to request, clamped against the captured original speed.</summary>
    private TimeControlEnum TargetSpeed(ThrottleLevel level)
    {
        switch (level)
        {
            case ThrottleLevel.Pause:
                return TimeControlEnum.Pause;
            case ThrottleLevel.SlowTo1x:
                // Cap fast-forward at 1x, but never speed a paused or already-slow game up.
                return originalSpeed == TimeControlEnum.Play_2x ? TimeControlEnum.Play_1x : originalSpeed;
            default:
                return originalSpeed;
        }
    }

    private static string ThrottleMessage(ThrottleLevel level, int catchingUp)
    {
        switch (level)
        {
            case ThrottleLevel.Pause:
                return $"{catchingUp} clients are catching up, pausing";
            case ThrottleLevel.SlowTo1x:
                return $"{catchingUp} clients are catching up, limiting to 1x";
            default:
                return "All clients synchronized, resuming";
        }
    }

    private bool IsClientCaughtUp(NetPeer peer)
    {
        if (peer.ConnectionState != ConnectionState.Connected) return true;

        var numPacketsInQueue = peer.GetPacketsCountInReliableQueue(0, true)
                              + peer.GetPacketsCountInReliableQueue(0, false);

        Logger.Information($"Peer {peer.Address} is catching up with {numPacketsInQueue} packets remaining");

        return numPacketsInQueue == 0;
    }

    /// <summary>
    /// Unpause policy: prevents leaving pause while any client is overloaded.
    /// </summary>
    /// <returns>True if unpausing is allowed, otherwise false</returns>
    internal bool PausePolicy()
    {
        lock (slowPeers)
        {
            if (pausePeers.Count > 0)
            {
                Logger.Information($"{pausePeers.Count} clients are overloaded, unable to unpause");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Fast-forward policy: prevents fast-forwarding while any client is catching up.
    /// </summary>
    /// <returns>True if fast-forwarding is allowed, otherwise false</returns>
    internal bool SlowDownPolicy()
    {
        lock (slowPeers)
        {
            if (slowPeers.Count > 0)
            {
                Logger.Information($"{slowPeers.Count} clients are catching up, unable to fast-forward");
                return false;
            }

            return true;
        }
    }

    private enum ThrottleLevel
    {
        None,
        SlowTo1x,
        Pause,
    }
}
