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

    /// <summary>Commits a concluded battle's result to the campaign on mission end (host only).</summary>
    public IBattleResultCommitter ResultCommitter { get; }

    private readonly IBattleInstanceLifecycle lifecycle;
    private readonly IOwnedAgentReplicator replicator;
    private readonly IAgentDeathReporter deathReporter;
    private readonly IPuppetSpawner puppetSpawner;
    private readonly IPuppetDeathApplier puppetDeathApplier;
    private readonly IBattleDamageRouter damageRouter;
    private readonly IBattleAuthorityMigrator authorityMigrator;
    private readonly IReinforcementFielder reinforcementFielder;
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
        puppetSpawner = new PuppetSpawner(messageBroker, objectManager, coopMissionComponent, session, casualties, deployment, formationAssigner);
        puppetDeathApplier = new PuppetDeathApplier(messageBroker, coopMissionComponent, casualties);
        damageRouter = new BattleDamageRouter(network, messageBroker, coopMissionComponent, session);
        authorityMigrator = new BattleAuthorityMigrator(relayNetwork, messageBroker, objectManager, playerManager, coopMissionComponent, session, casualties, deployment, formationAssigner);
        reinforcementFielder = new ReinforcementFielder(messageBroker, objectManager, session, deployment, formationAssigner);
        supplyReporter = new SupplyProgressReporter(relayNetwork, session);

        Session = session;
        Deployment = deployment;
        ResultCommitter = new BattleResultCommitter(objectManager, session, hostRegistry);
    }

    public override void Dispose()
    {
        lifecycle.Dispose();
        replicator.Dispose();
        deathReporter.Dispose();
        puppetSpawner.Dispose();
        puppetDeathApplier.Dispose();
        damageRouter.Dispose();
        authorityMigrator.Dispose();
        reinforcementFielder.Dispose();
        Deployment.Dispose();

        base.Dispose();
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        puppetSpawner.DrainPendingPuppets();
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

        // ...and on the live battle itself: replay the agents WE own so the joiner spawns matching puppets.
        replicator.ReplicateCurrentAgentsTo(controllerId);
    }

    protected override void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo)
    {
        // Log receipt to confirm the bidirectional handshake; the joiner catch-up itself happens on the
        // owners' side (SendJoinInfo -> ReplicateCurrentAgentsTo).
        Logger.Information("[BattleSync] Received join info from {Controller} (instance {Instance})", joinInfo.ControllerId, Session.InstanceId);
    }

    protected override void OnLeaving()
    {
        // Commit the concluded battle's result to the campaign BEFORE tearing the instance down, so the server
        // captures losers / awards the win and finalizes the encounter.
        ResultCommitter.CommitIfHost();

        lifecycle.Leave();
    }
}
