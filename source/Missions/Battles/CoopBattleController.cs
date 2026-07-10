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
/// <item><see cref="BattleAuthorityMigrator"/> — retreat despawn vs. disconnect adoption, host migration.</item>
/// <item><see cref="ReinforcementFielder"/> — the host fields new AI parties mid-battle.</item>
/// <item><see cref="SupplyProgressReporter"/> / <see cref="BattleResultCommitter"/> — server ledger + result commit.</item>
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

    /// <summary>Commits a concluded battle's result to the campaign on mission end.</summary>
    public IBattleResultCommitter ResultCommitter { get; }

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
    private readonly ISiegeEngineStateReporter siegeEngineStateReporter;
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
        IAgentFormationAssigner formationAssigner)
        : base(network, messageBroker, objectManager, coopMissionComponent)
    {
        var session = new BattleSession(controllerIdProvider, hostRegistry);
        var casualties = new CasualtyAttributionMap();

        var deployment = new BattleDeploymentCoordinator(network, messageBroker, session);

        lifecycle = new BattleInstanceLifecycle(network, relayNetwork, messageBroker, objectManager, coopMissionComponent, session);
        replicator = new OwnedAgentReplicator(network, messageBroker, objectManager, coopMissionComponent, session, casualties, deployment);
        deathReporter = new AgentDeathReporter(network, relayNetwork, messageBroker, objectManager, coopMissionComponent, session, casualties);
        routReporter = new AgentRoutReporter(network, messageBroker, coopMissionComponent, session, casualties);
        puppetSpawner = new PuppetSpawner(messageBroker, objectManager, coopMissionComponent, session, casualties, deployment, formationAssigner);
        puppetDeathApplier = new PuppetDeathApplier(messageBroker, coopMissionComponent, casualties);
        puppetRoutApplier = new PuppetRoutApplier(messageBroker, coopMissionComponent, casualties);
        damageRouter = new BattleDamageRouter(network, messageBroker, coopMissionComponent, session);
        authorityMigrator = new BattleAuthorityMigrator(relayNetwork, messageBroker, objectManager, playerManager, coopMissionComponent, session, casualties, deployment, formationAssigner);
        reinforcementFielder = new ReinforcementFielder(messageBroker, objectManager, session, deployment, formationAssigner);
        siegeEngineDeployment = new SiegeEngineDeploymentReplicator(network, messageBroker, session);
        siegeMachineState = new SiegeMachineStateReplicator(network, messageBroker, session, coopMissionComponent.AgentRegistry);
        siegeWeaponFire = new SiegeWeaponFireReplicator(network, messageBroker, coopMissionComponent.AgentRegistry);
        supplyReporter = new SupplyProgressReporter(relayNetwork, session);

        hostRegistryRef = hostRegistry;
        Session = session;
        Deployment = deployment;
        ResultCommitter = new BattleResultCommitter(objectManager, session, hostRegistry);
        siegeEngineStateReporter = new SiegeEngineStateReporter(objectManager, session, hostRegistry, relayNetwork);
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
        BattleConclusionGate.IsLocalBattleHost = false;

        base.Dispose();
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

        // Only the battle host's mission conclusion may relay to the server (every coop battle mission).
        BattleConclusionGate.IsInCoopBattleMission = true;
        BattleConclusionGate.IsLocalBattleHost = Session.IsLocalHost;

        // Vanilla's end checks unlock at the LOCAL deployment finish, but a peer's enemies arrive as
        // the battle host's puppets — a peer committing before the host has fielded anything reads an
        // empty enemy side as "the enemy ran away" and concludes a bogus victory. Hold the end
        // conditions until the battle is live (the host released the NPC AI); vanilla resumes then.
        if (!endConditionHoldReleased)
        {
            var battleEndLogic = Mission?.GetMissionBehavior<BattleEndLogic>();
            if (battleEndLogic != null)
            {
                bool battleLive = Deployment.IsActivated;
                battleEndLogic.ChangeCanCheckForEndCondition(battleLive);
                if (battleLive) endConditionHoldReleased = true;
            }
        }

        puppetSpawner.DrainPendingPuppets();
        siegeEngineDeployment.DrainPending();
        siegeMachineState.Tick(dt);
        diagnostics.Tick(dt);
        supplyReporter.Tick(dt);
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
        // Report engine states before the commit, so the server applies them while the siege still exists.
        siegeEngineStateReporter.ReportIfHost();

        // Commit the concluded battle's result to the campaign BEFORE tearing the instance down, so the server
        // captures losers / awards the win and finalizes the encounter.
        ResultCommitter.CommitResolvedResult();

        lifecycle.Leave();
    }
}
