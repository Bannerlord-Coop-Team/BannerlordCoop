using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Missions.Data;
using Missions.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Per-mission P2P controller for a field battle — the battle counterpart to
/// <see cref="Taverns.CoopLocationsController"/>. Attached to a freshly opened battle mission by
/// <see cref="CoopBattleBehaviorAttacher"/> (from the coop launcher, or from <c>BattleMissionEntryPatch</c>
/// on the native OpenBattleMission path).
/// <para>
/// This class is the COMPOSITION ROOT only: it wires the per-battle components (each owning its message
/// subscriptions and one responsibility) around the shared <see cref="BattleSession"/> and
/// <see cref="CasualtyAttributionMap"/>, and fans the native <see cref="TaleWorlds.MountAndBlade.MissionBehavior"/>
/// lifecycle out to them. The behavior itself lives in the parts:
/// <list type="bullet">
/// <item><see cref="BattleInstanceLifecycle"/> — mesh connect + relay announce on entry/leave.</item>
/// <item><see cref="OwnedAgentReplicator"/> — owner-side spawn capture, joiner catch-up, deployment reveal.</item>
/// <item><see cref="AgentDeathReporter"/> / <see cref="PuppetDeathApplier"/> — owner death broadcast + server
/// casualty; peer-side puppet kill.</item>
/// <item><see cref="PuppetSpawner"/> — peer-side puppet spawn/buffer/drain.</item>
/// <item><see cref="BattleDamageRouter"/> — puppet hits routed to and applied by the owner.</item>
/// <item><see cref="BattleAuthorityMigrator"/> — player-party withdrawal and host migration.</item>
/// <item><see cref="ReinforcementFielder"/> — the host fields new AI parties mid-battle.</item>
/// <item><see cref="SupplyProgressReporter"/> / <see cref="BattleResultCommitter"/> — server ledger + result report.</item>
/// <item><see cref="BattleDeploymentCoordinator"/> — deployment activation ("any client" NPC release) and the
/// own-party reveal gate.</item>
/// </list>
/// </para>
/// </summary>
public class CoopBattleController : CoopMissionController
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleController>();

    /// <summary>Shared per-battle context: instance id, own controller id, host checks.</summary>
    public IBattleSession Session { get; }

    /// <summary>Deployment activation + reveal state (exposed for the join catch-up and tests).</summary>
    public IBattleDeploymentCoordinator Deployment { get; }

    /// <summary>Reports a concluded battle's result to the campaign server.</summary>
    public IBattleResultCommitter ResultCommitter { get; }

    /// <summary>Reports final siege engine state before the shared result is applied.</summary>
    public ISiegeEngineStateReporter SiegeEngineStateReporter { get; }

    private readonly IBattleInstanceLifecycle lifecycle;
    private readonly IOwnedAgentReplicator replicator;
    private readonly IAgentDeathReporter deathReporter;
    private readonly IAgentRoutReporter routReporter;
    private readonly IPuppetSpawner puppetSpawner;
    private readonly IPuppetDeathApplier puppetDeathApplier;
    private readonly IPuppetRoutApplier puppetRoutApplier;
    private readonly IBattleDamageRouter damageRouter;
    private readonly IBattleAuthorityMigrator authorityMigrator;
    private readonly IReinforcementFielder reinforcementFielder;
    private readonly ISiegeEngineDeploymentReplicator siegeEngineDeployment;
    private readonly ISiegeMachineStateReplicator siegeMachineState;
    private readonly ISiegeWeaponFireReplicator siegeWeaponFire;
    private readonly IBattleHostRegistry hostRegistryRef;

    // Whether the pre-live hold on vanilla's battle-end checks has been lifted (see OnMissionTick).
    private bool endConditionHoldReleased;
    private readonly ISupplyProgressReporter supplyReporter;
    private readonly BattleTeamDiagnostics diagnostics = new BattleTeamDiagnostics();

    public CoopBattleController(
        IBattleNetwork network,
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleHostRegistry hostRegistry,
        IAgentFormationAssigner formationAssigner,
        IMissionContext missionContext,
        IHostEpochPolicy hostEpochPolicy)
        : base(network, messageBroker, objectManager, coopMissionComponent)
    {
        var session = new BattleSession(controllerIdProvider, hostRegistry);
        var casualties = new CasualtyAttributionMap();

        var deployment = new BattleDeploymentCoordinator(network, messageBroker, session);

        lifecycle = new BattleInstanceLifecycle(network, relayNetwork, messageBroker, objectManager, coopMissionComponent, session, missionContext);
        replicator = new OwnedAgentReplicator(network, messageBroker, objectManager, coopMissionComponent, session, casualties, deployment);
        deathReporter = new AgentDeathReporter(network, relayNetwork, messageBroker, objectManager, coopMissionComponent, session, casualties);
        routReporter = new AgentRoutReporter(network, messageBroker, coopMissionComponent, session, casualties);
        puppetSpawner = new PuppetSpawner(messageBroker, objectManager, playerManager, coopMissionComponent, session, casualties, deployment, formationAssigner);
        puppetDeathApplier = new PuppetDeathApplier(messageBroker, coopMissionComponent, casualties);
        puppetRoutApplier = new PuppetRoutApplier(messageBroker, coopMissionComponent, casualties);
        damageRouter = new BattleDamageRouter(network, messageBroker, coopMissionComponent, session);
        authorityMigrator = new BattleAuthorityMigrator(relayNetwork, messageBroker, objectManager, playerManager, coopMissionComponent, session, casualties, deployment, formationAssigner, missionContext);
        reinforcementFielder = new ReinforcementFielder(messageBroker, objectManager, coopMissionComponent, session, deployment, formationAssigner, casualties);
        // BR-102: ONE host-epoch policy shared by both siege replicators, so its accepted-epoch
        // watermark spans every host-authority message type (engine placement + machine state/authority)
        // — a superseded hosting generation is dropped consistently across both. The policy is a
        // per-battle transient (see MissionModule), so this controller's per-battle lifetime resets it.
        siegeEngineDeployment = new SiegeEngineDeploymentReplicator(network, messageBroker, session, hostEpochPolicy);
        siegeMachineState = new SiegeMachineStateReplicator(network, messageBroker, session, coopMissionComponent.AgentRegistry, hostEpochPolicy);
        siegeWeaponFire = new SiegeWeaponFireReplicator(network, messageBroker, coopMissionComponent.AgentRegistry);
        supplyReporter = new SupplyProgressReporter(relayNetwork, session);

        hostRegistryRef = hostRegistry;
        Session = session;
        Deployment = deployment;
        ResultCommitter = new BattleResultCommitter(relayNetwork, session);
        SiegeEngineStateReporter = new SiegeEngineStateReporter(objectManager, session, hostRegistry, relayNetwork);

        // Decode order clips during battle setup so the first issued order does not hitch.
        coopMissionComponent.AgentVoiceHandler.WarmUp();
    }

    public override void Dispose()
    {
        lifecycle.Dispose();
        replicator.Dispose();
        deathReporter.Dispose();
        routReporter.Dispose();
        puppetSpawner.Dispose();
        puppetDeathApplier.Dispose();
        puppetRoutApplier.Dispose();
        damageRouter.Dispose();
        authorityMigrator.Dispose();
        reinforcementFielder.Dispose();
        siegeEngineDeployment.Dispose();
        siegeMachineState.Dispose();
        siegeWeaponFire.Dispose();
        Deployment.Dispose();

        // OnMissionTick sets these each frame; reset them here (their owner) so a stale authority
        // never bleeds into the next siege before the first tick refreshes it.
        SiegeMissionAuthorityGate.IsLocalAuthority = false;
        SiegeMissionAuthorityGate.IsAuthorityKnown = false;
        SiegeMissionAuthorityGate.ResetClaimedMachines();
        BattleConclusionGate.IsInCoopBattleMission = false;

        base.Dispose();
    }

    // MISSION-READY (BR-010): the native MissionState.FinishMissionLoading fans Mission.AfterStart() out to
    // the behaviors only once Mission.IsLoadingFinished turned true, so this is the moment this client has
    // FINISHED LOADING the battle mission. Announce it so BattleHostHandler requests the host election —
    // the server's per-battle order becomes the mission-ready order (BR-013), not the entry order. The
    // session began at entry (PlayerEnteredBattle -> BattleInstanceLifecycle), well before loading finishes.
    public override void AfterStart()
    {
        base.AfterStart();

        BattleConclusionGate.IsInCoopBattleMission = true;

        // BR-025: the deployment time limit begins when this player becomes mission-ready — right here.
        Deployment.OnMissionReady();

        if (Session.HasInstance)
            messageBroker.Publish(this, new BattleMissionReady(Session.InstanceId));
        else
            Logger.Warning("[BattleHost] Battle mission finished loading with no instance session — cannot announce mission-ready");
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        // The mission host is the single siege authority (engine deployment and machine simulation);
        // host election can settle after the mission opens, so keep the patch-visible flags current
        // instead of latching them once. Siege missions only, so field battles never touch the gate.
        if (Mission?.IsSiegeBattle == true)
        {
            SiegeMissionAuthorityGate.IsLocalAuthority = Session.IsLocalHost;
            SiegeMissionAuthorityGate.IsAuthorityKnown = hostRegistryRef.TryGet(Session.InstanceId, out _);
        }

        // Route coop mission victories through the server's result-ready completion barrier.
        BattleConclusionGate.IsInCoopBattleMission = true;

        // Register the buffered puppet batch before the one-shot end-condition gate so it can observe both
        // sides as fielded even when a queued terminal event removes every agent on one side this tick.
        puppetSpawner.DrainPendingPuppets();
        reinforcementFielder.Tick();

        // Vanilla's end checks unlock at the LOCAL deployment finish, but a side whose troops arrive as
        // another client's puppets can be empty long after activation (own-party troops stay withheld
        // until their owner's commit), and the retreat check latches "everyone ran away" on an empty
        // side. Hold the end conditions until the battle is live AND both sides actually field a live
        // agent here. The only exception is a side whose reserve crossed the spawn handler's intentional
        // timeout fallback; that exact side is allowed to start empty so the battle can conclude instead of
        // wedging forever. The release is one-shot, so a later real depletion still concludes normally.
        if (!endConditionHoldReleased)
        {
            var battleEndLogic = Mission?.GetMissionBehavior<BattleEndLogic>();
            if (battleEndLogic != null)
            {
                bool battleLive = BattleReadyForEndChecks();
                battleEndLogic.ChangeCanCheckForEndCondition(battleLive);
                if (battleLive) endConditionHoldReleased = true;
            }
        }

        // Terminal events can now remove freshly registered puppets without preventing the one-shot gate
        // above from releasing. Vanilla end checks will observe the resulting depletion normally.
        puppetDeathApplier.DrainPendingDeaths();
        puppetRoutApplier.DrainPendingRouts();

        siegeEngineDeployment.DrainPending(dt);
        siegeMachineState.Tick(dt);
        diagnostics.Tick(dt);
        supplyReporter.Tick(dt);

        // BR-025: expire the local deployment time limit (auto-finishes deployment via the native Start
        // Battle path when the game-configured limit elapses; a no-op once deployment has finished).
        Deployment.Tick(dt);
    }

    public override void OnPreDisplayMissionTick(float dt)
    {
        base.OnPreDisplayMissionTick(dt);
        damageRouter.Tick(dt);
    }

    // A side counts as fielded once some team of it has a live human agent (puppets qualify; they join
    // teams like any agent). Mirrors CoopBattleDepletionPatch's live-agent count.
    private bool BattleReadyForEndChecks()
    {
        bool attackerFielded = false;
        bool defenderFielded = false;
        foreach (var team in Mission.Teams)
        {
            if (team.Side != BattleSideEnum.Attacker && team.Side != BattleSideEnum.Defender) continue;
            if (team.Side == BattleSideEnum.Attacker && attackerFielded) continue;
            if (team.Side == BattleSideEnum.Defender && defenderFielded) continue;

            foreach (var agent in team.ActiveAgents)
            {
                if (!agent.IsHuman) continue;
                if (team.Side == BattleSideEnum.Attacker) attackerFielded = true;
                else defenderFielded = true;
                break;
            }

            if (attackerFielded && defenderFielded) break;
        }

        return ShouldReleaseEndConditionHold(
            Deployment.IsActivated,
            attackerFielded,
            defenderFielded,
            BattleSpawnGate.IsMissingReserveSideAccepted(BattleSideEnum.Attacker),
            BattleSpawnGate.IsMissingReserveSideAccepted(BattleSideEnum.Defender));
    }

    /// <summary>
    /// Pure release rule for the pre-live end-condition hold. A timeout is a substitute only for the side
    /// whose reserve was deliberately abandoned; it cannot hide that the other side has not fielded yet.
    /// </summary>
    public static bool ShouldReleaseEndConditionHold(
        bool deploymentActivated,
        bool attackerFielded,
        bool defenderFielded,
        bool attackerMissingReserveAccepted,
        bool defenderMissingReserveAccepted)
    {
        return deploymentActivated
            && (attackerFielded || defenderFielded)
            && (attackerFielded || attackerMissingReserveAccepted)
            && (defenderFielded || defenderMissingReserveAccepted);
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);

        deathReporter.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);
    }

    // The local player just finished their own deployment (Start Battle): the coordinator announces it to the
    // mesh (and marks the battle live if we are the host); on the FIRST commit we reveal the withheld own-party
    // troops at their deployed positions (requirement #4) — inline, on this same game-thread call, before the
    // native un-pause moves them.
    public override void OnDeploymentFinished()
    {
        base.OnDeploymentFinished();

        siegeEngineDeployment.MarkLocalDeploymentFinished();

        if (Deployment.OnLocalDeploymentFinished())
            replicator.BroadcastOwnDeployedTroops();
    }

    protected override void SendJoinInfo(string controllerId)
    {
        // Announce presence so the handshake completes and the P2P link is proven.
        var joinInfo = new NetworkMissionJoinInfo(
            Session.OwnControllerId,
            isPlayerAlive: true,
            aiAgentDatas: Array.Empty<CoopAgentSpawnData>());

        network.Send(controllerId, joinInfo);
        Logger.Information("[BattleSync] Sent join info to {Controller} for instance {Instance}", controllerId, Session.InstanceId);

        // Catch a mid-battle joiner up on the activation state (a no-op while the battle is not live)...
        Deployment.CatchUpJoiner(controllerId);

        // ...and on the live battle itself: replay the agents WE own so the joiner spawns matching puppets,
        // plus the siege engine placement when we are the deployer (a no-op in field battles).
        replicator.ReplicateCurrentAgentsTo(controllerId);
        siegeEngineDeployment.CatchUpJoiner(controllerId);
        siegeMachineState.CatchUpJoiner(controllerId);
    }

    protected override void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo)
    {
        // Log receipt to confirm the bidirectional handshake; the joiner catch-up itself happens on the
        // owners' side (SendJoinInfo -> ReplicateCurrentAgentsTo).
        Logger.Information("[BattleSync] Received join info from {Controller} (instance {Instance})", joinInfo.ControllerId, Session.InstanceId);
    }

    protected override void OnLeaving()
    {
        damageRouter.FlushForMissionEnd();

        // Retreats have no result-ready callback, so report their siege state during teardown.
        SiegeEngineStateReporter.ReportOnLeavingIfHost();

        // Retry the result-ready report before tearing the instance down. Duplicate reports are idempotent.
        ResultCommitter.CommitResolvedResult();

        lifecycle.Leave();
    }
}
