using Common.Logging;
using Common.Messaging;
using GameInterface.Missions.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions;

/// <summary>
/// Shared base for the per-mission P2P controllers (taverns, battles). Owns what every coop mission needs:
/// the battle network, the agent registry, the object manager, the set of per-mission sync handlers, and
/// the join-info handshake wiring — announce ourselves when a peer connects, and process a peer's
/// <see cref="NetworkMissionJoinInfo"/> when it arrives. Subclasses supply the mission-specific behaviour:
/// how to build their own join info, how to spawn a peer's agents, plus any extra subscriptions
/// (overriding <see cref="Dispose"/>) and leave logic (overriding <see cref="OnLeaving"/>).
/// </summary>
public abstract class CoopMissionController : MissionBehavior, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopMissionController>();

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    protected readonly IBattleNetwork network;
    protected readonly IMessageBroker messageBroker;
    protected readonly IObjectManager objectManager;
    protected readonly ICoopMissionComponent coopMissionComponent;

    protected CoopMissionController(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;

        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_MissionPeerEntered);
        messageBroker.Subscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
    }

    public virtual void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_MissionPeerEntered);
        messageBroker.Unsubscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
    }

    private void Handle_MissionPeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        // Server-mediated replacement for PeerConnected: a controller entered our instance (the notification
        // arrived over the campaign/relay connection), so send it our join info over the mesh.
        SendJoinInfo(payload.What.ControllerId);
    }

    private void Handle_JoinInfo(MessagePayload<NetworkMissionJoinInfo> payload)
    {
        HandleJoinInfo((NetPeer)payload.Who, payload.What);
    }

    /// <summary>Build and send this client's join info to <paramref name="controllerId"/>, a controller that just entered our instance.</summary>
    protected abstract void SendJoinInfo(string controllerId);

    /// <summary>Process <paramref name="peer"/>'s join info — spawn and register its agents in this mission.</summary>
    protected abstract void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo);

    /// <summary>
    /// Hook for subclass leave logic (announce departure, stop the socket) before the mission tears down.
    /// Runs at the start of <see cref="OnEndMissionInternal"/>, before the base teardown. Empty by default.
    /// </summary>
    protected virtual void OnLeaving() { }

    public override void OnEndMissionInternal()
    {
        OnLeaving();

        base.OnEndMission();
        Dispose();
    }
}
