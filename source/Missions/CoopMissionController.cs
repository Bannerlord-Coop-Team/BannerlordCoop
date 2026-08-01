using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Missions.Agents.Handlers;
using Missions.Battles;
#if DEBUG
using Missions.Diagnostics;
#endif
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
    internal IAgentMovementHandler AgentMovementHandler =>
        coopMissionComponent.AgentMovementHandler;

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

    public override void OnPreMissionTick(float dt)
    {
        base.OnPreMissionTick(dt);

        // Agent ticking follows OnMissionTick and can replace the retained look before the next collision window.
        // Restore the owner frame before its guard so native collision reads one coherent input.
        coopMissionComponent.AgentMovementHandler.Interpolator
            .ReplayLookDirections();
        coopMissionComponent.AgentActionHandler.ApplyRemoteGuardStates();
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        // Capture continuous movement on the game thread so each snapshot reads one coherent engine state. A
        // background poll can race native movement/input updates and send a moved position with stale input,
        // leaving peers to interpolate a sliding puppet with no locomotion animation.
        coopMissionComponent.AgentMovementHandler.PollMovement(dt);

        // Smoothly reconcile received puppets toward their owners' last-reported positions every frame; the
        // per-packet correction was bound to the bursty movement-poll cadence and looked stepped. Subclasses that
        // override OnMissionTick call base (CoopBattleController does), and CoopLocationsController does not
        // override it, so this runs for both battle and location missions.
        coopMissionComponent.AgentMovementHandler.Interpolator.Tick(dt);

        // Continuous movement setters can replace defend input after the mission boundary. Restore the held
        // flags without restarting the guard command so native animation keeps its own timeline.
        coopMissionComponent.AgentActionHandler.RefreshRemoteGuardStatesAfterMovement();

        // Capture the main player's raw defend input before native Agent processing can rewrite it.
        coopMissionComponent.AgentActionHandler.PollActions();

        coopMissionComponent.AgentVoiceHandler.PollVoices();
        coopMissionComponent.MissileHandler.DrainPendingShots();
    }

    public override void OnPreDisplayMissionTick(float dt)
    {
        base.OnPreDisplayMissionTick(dt);

        // Native Agent processing can rewrite a puppet's look after OnMissionTick replayed it.
        // Restore only that display input here; movement setters can consume the active guard flags.
        coopMissionComponent.AgentMovementHandler.Interpolator
            .ReplayLookDirections();

        // Native Agent processing realizes authoritative AI and player actions after the input boundary.
        // Diff them here so peers receive the displayed action instead of retaining an earlier pose.
        // A headless mission participant would need an equivalent non-display boundary for AI action sync.
        coopMissionComponent.AgentActionHandler.PollActionsAfterNativeTick();

        // A 30 FPS battle needs a second game-thread sample to reach the 60 Hz player target. Consume only
        // accrued budget here, after the native agent tick can produce a newer movement frame.
        coopMissionComponent.AgentMovementHandler.PollPlayerMovement();

        coopMissionComponent.AgentMovementHandler
            .ReplaySyntheticMountTurnAnimationsAfterNativeTick();

        // Keep short remote guard reactions visible for this frame without driving held guard actions.
        coopMissionComponent.AgentActionHandler.ReplayRemoteGuardReactions();
#if DEBUG
        MissionActionDiagnostics.SampleAnimations(
            coopMissionComponent.AgentRegistry);
#endif
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
        string controllerId = payload.What.ControllerId;
        SendJoinInfo(controllerId);
        coopMissionComponent.AgentActionHandler.CatchUpJoiner(controllerId);
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
        // Detach the per-mission agent handlers FIRST, before mission state and native agents are freed. Both
        // detach deterministically here instead of leaking their packet-handler registration until the GC
        // finalizer runs.
        coopMissionComponent.AgentMovementHandler.Dispose();
        coopMissionComponent.AgentActionHandler.Dispose();
        coopMissionComponent.AgentVoiceHandler.Dispose();

        coopMissionComponent.MissileHandler.Dispose();
        coopMissionComponent.WeaponDropHandler.Dispose();
        coopMissionComponent.WeaponPickupHandler.Dispose();
        coopMissionComponent.ShieldDamageHandler.Dispose();
        coopMissionComponent.CombatHitPresentationHandler.Dispose();
        coopMissionComponent.AgentDeathHandler.Dispose();

        OnLeaving();

        base.OnEndMission();
        Dispose();
    }
}
