using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Missions.Messages;
using Serilog;
using System; 
using TaleWorlds.MountAndBlade;

namespace Missions;

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

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        // Smoothly reconcile received puppets toward their owners' last-reported positions every frame; the
        // per-packet correction was bound to the bursty ~10ms poll cadence and looked stepped. Subclasses that
        // override OnMissionTick call base (CoopBattleController does), and CoopLocationsController does not
        // override it, so this runs for both battle and location missions.
        coopMissionComponent.AgentMovementHandler.Interpolator.Tick(dt);

        // Capture discrete action changes on the GAME thread (attacks, jumps, gestures...): a one-frame action
        // transition can't be observed reliably from the background movement poller, so actions are event-synced
        // from here instead of polled with movement.
        coopMissionComponent.AgentActionHandler.PollActions();
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
        // Detach the per-mission agent handlers FIRST. The movement handler stops its background poller before
        // anything else tears down, so the poll loop isn't reading agents/mission state as they are freed (it
        // races the game thread and crashes on freed native agents). Both detach deterministically here instead
        // of leaking their poller/packet-handler registration until the GC finalizer runs.
        coopMissionComponent.AgentMovementHandler.Dispose();
        coopMissionComponent.AgentActionHandler.Dispose();

        // Detach the remaining per-mission sync handlers too. They only unsubscribe from the broker in Dispose
        // (no poller), so without this their stale subscriptions from a torn-down mission keep handling messages
        // alongside the next mission's fresh handlers until the GC finalizer runs.
        coopMissionComponent.MissileHandler.Dispose();
        coopMissionComponent.WeaponDropHandler.Dispose();
        coopMissionComponent.WeaponPickupHandler.Dispose();
        coopMissionComponent.ShieldDamageHandler.Dispose();
        coopMissionComponent.AgentDeathHandler.Dispose();

        OnLeaving();

        base.OnEndMission();
        Dispose();
    }
}
