using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Registry;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Sessions.Messages;
using LiteNetLib;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using Missions.Services.Network;
using Missions.Sessions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Sessions;

/// <summary>
/// Per-mission P2P controller for a campaign-free session: the battle replication parts without a campaign,
/// map event, deployment phase or hero.
/// <para>The tick, join replay and leave code is copied from <c>CoopBattleController</c>, so a fix there needs a copy here.</para>
/// </summary>
internal class CoopSessionController : CoopMissionController
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopSessionController>();

    /// <summary>Shared per-session context: instance id, own controller id, host checks.</summary>
    public IBattleSession Session { get; }

    private readonly SessionMissionContext context;
    private readonly INetwork relayNetwork;
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
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IBattleHostRegistry hostRegistryRef;
    private readonly IRegistryManager registryManager;
    private string lastHost;
    private int lastEpoch = -1;

    public CoopSessionController(
        IBattleNetwork network,
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ICoopMissionComponent coopMissionComponent,
        INetworkWorldItemRegistry worldItemRegistry,
        IBattleHostRegistry hostRegistry,
        IAgentFormationAssigner formationAssigner,
        IMissionContext missionContext,
        IHostEpochPolicy hostEpochPolicy,
        IBattleAgentBudget agentBudget,
        IGuardedHitWindow guardedHitWindow,
        IAgentNativeMountState agentNativeMountState,
        IPuppetMountStateRepairer puppetMountStateRepairer,
        IBattleAgentSpawnBatchCodec spawnBatchCodec,
        IBattleDamageDataMapper battleDamageDataMapper,
        IMissionWeaponDataMapper missionWeaponDataMapper,
        ISiegeGateHitApplier siegeGateHitApplier,
        IRangedSiegeWeaponPresentation rangedSiegeWeaponPresentation,
        IBattleTeamResolver teamResolver,
        IBattleAgentFader agentFader,
        IBattlePlayerPartyResolver playerParties,
        SessionBattleCharacterResolver characterResolver,
        SessionPuppetOriginFactory puppetOrigins,
        SessionMissionContext context,
        IRegistryManager registryManager)
        : base(
            network,
            messageBroker,
            objectManager,
            coopMissionComponent,
            Missions.Agents.Handlers.MovementCadenceProfile.Battle)
    {
        this.context = context;
        this.relayNetwork = relayNetwork;
        hostRegistryRef = hostRegistry;
        this.registryManager = registryManager;

        var session = new BattleSession(controllerIdProvider, hostRegistry);
        Session = session;
        coopMissionComponent.WeaponDropHandler.ConfigureLocalHostProvider(
            () => session.IsLocalHost);
        var casualties = new CasualtyAttributionMap();

        deployment = new SessionDeploymentCoordinator();

        lifecycle = new BattleInstanceLifecycle(
            network,
            relayNetwork,
            messageBroker,
            objectManager,
            coopMissionComponent,
            worldItemRegistry,
            session,
            missionContext);
        deathReporter = new AgentDeathReporter(network, relayNetwork, messageBroker, coopMissionComponent, session, casualties);
        routReporter = new AgentRoutReporter(network, messageBroker, coopMissionComponent, session, casualties, lifecycle);
        puppetRoutApplier = new PuppetRoutApplier(messageBroker, coopMissionComponent, casualties, agentFader);
        puppetDeathApplier = new PuppetDeathApplier(
            messageBroker,
            coopMissionComponent,
            casualties,
            puppetMountStateRepairer);
        damageRouter = new BattleDamageRouter(
            network,
            messageBroker,
            coopMissionComponent,
            session,
            guardedHitWindow,
            agentNativeMountState,
            puppetMountStateRepairer,
            battleDamageDataMapper);
        // The migrator needs the fielder; a session never ticks it, so it fields nothing.
        reinforcementFielder = new ReinforcementFielder(messageBroker, objectManager, coopMissionComponent, session, deployment, formationAssigner, casualties, agentBudget);
        OwnedAgentReplicator ownedAgentReplicator = null;
        Action<IReadOnlyCollection<Guid>> authorityChanged = changedAgentIds => ownedAgentReplicator.BroadcastAuthorityRefresh(changedAgentIds);
        var troopWithdrawal = new BattleTroopWithdrawal(coopMissionComponent, lifecycle, casualties);
        var partyWithdrawal = new BattlePartyWithdrawal(coopMissionComponent, session, troopWithdrawal, agentFader, playerParties,
            authorityChanged);
        authorityMigrator = new BattleAuthorityMigrator(relayNetwork, messageBroker, objectManager, playerManager, coopMissionComponent, session, deployment, formationAssigner, missionContext, reinforcementFielder,
            partyWithdrawal, playerParties, authorityChanged);
        ownedAgentReplicator = new OwnedAgentReplicator(
            network,
            messageBroker,
            objectManager,
            coopMissionComponent,
            session,
            casualties,
            deployment,
            spawnBatchCodec,
            missionWeaponDataMapper,
            authorityMigrator,
            characterResolver: characterResolver);
        replicator = ownedAgentReplicator;
        puppetSpawner = new PuppetSpawner(
            messageBroker,
            objectManager,
            playerManager,
            coopMissionComponent,
            session,
            casualties,
            deployment,
            formationAssigner,
            agentBudget,
            missionWeaponDataMapper,
            puppetRoutApplier,
            spawnBatchCodec,
            authorityMigrator,
            teamResolver,
            characterResolver: characterResolver,
            puppetOrigins: puppetOrigins);
        siegeEngineDeployment = new SiegeEngineDeploymentReplicator(network, messageBroker, session, hostEpochPolicy);
        siegeMachineState = new SiegeMachineStateReplicator(network, messageBroker, session, coopMissionComponent.AgentRegistry, hostEpochPolicy, rangedSiegeWeaponPresentation);
        siegeWeaponFire = new SiegeWeaponFireReplicator(network, messageBroker, coopMissionComponent.AgentRegistry, session, siegeMachineState, siegeGateHitApplier, hostEpochPolicy, rangedSiegeWeaponPresentation);

        messageBroker.Subscribe<BattleHostAssignmentApplied>(Handle_BattleHostAssigned);
        messageBroker.Subscribe<NetworkEndBattleMission>(Handle_EndMission);
    }

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();
        // The mod's Game exists now; its items get the ids the weapon sync packs.
        registryManager.RegisterMissionItemObjects();
        lifecycle.Enter(context.SessionId);
    }

    // The mission finished loading: ask the server for the session host. No BattleMissionReady, so the client
    // battle host handler never asks for a battle host of its own.
    public override void AfterStart()
    {
        base.AfterStart();

        if (!Session.HasInstance)
        {
            Logger.Warning("[Session] Mission finished loading with no instance session, cannot ask for the host");
            return;
        }

        coopMissionComponent.AgentActionHandler.BindPilotSeats(Session.InstanceId, siegeMachineState);
        relayNetwork.SendAll(new NetworkRequestSessionHost(Session.InstanceId));
    }

    // Copy of the CoopBattleController tick without results, supply, deployment, diagnostics, end checks and handoffs.
    public override void OnMissionTick(float dt)
    {
        // Reliable spawn work is queued before this frame's unreliable movement traffic.
        replicator.FlushPendingSpawns();
        replicator.FlushPendingFormations();
        base.OnMissionTick(dt);

        if (Mission?.IsSiegeBattle == true)
        {
            SiegeMissionAuthorityGate.IsLocalAuthority = Session.IsLocalHost;
            SiegeMissionAuthorityGate.IsAuthorityKnown = hostRegistryRef.TryGet(Session.InstanceId, out _);
        }

        puppetSpawner.DrainPendingPuppets();
        puppetDeathApplier.DrainPendingDeaths();
        puppetRoutApplier.DrainPendingRouts();

        siegeEngineDeployment.DrainPending(dt);
        coopMissionComponent.WeaponPickupHandler.RetryPendingSiegeGrants();
        siegeMachineState.Tick(dt);
        siegeWeaponFire.Tick(dt);
    }

    public override void OnPreDisplayMissionTick(float dt)
    {
        base.OnPreDisplayMissionTick(dt);
        damageRouter.Tick(dt);
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);

        deathReporter.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);
        puppetRoutApplier.ForgetRemovedAgent(affectedAgent);
    }

    public override void OnAgentFleeing(Agent affectedAgent)
    {
        base.OnAgentFleeing(affectedAgent);
        routReporter.OnAgentFleeing(affectedAgent);
    }

    public override void OnScoreHit(
        Agent affectedAgent,
        Agent affectorAgent,
        WeaponComponentData attackerWeapon,
        bool isBlocked,
        bool isSiegeEngineHit,
        in Blow blow,
        in AttackCollisionData collisionData,
        float damagedHp,
        float hitDistance,
        float shotDifficulty)
    {
        base.OnScoreHit(
            affectedAgent,
            affectorAgent,
            attackerWeapon,
            isBlocked,
            isSiegeEngineHit,
            in blow,
            in collisionData,
            damagedHp,
            hitDistance,
            shotDifficulty);
        coopMissionComponent.CombatHitPresentationHandler.BroadcastAcceptedMeleeBlood(
            affectedAgent,
            affectorAgent,
            in blow,
            in collisionData);
        coopMissionComponent.AgentActionHandler.ObserveBlockedHit(
            affectedAgent,
            affectorAgent,
            isBlocked,
            in blow,
            in collisionData);
    }

    protected override void SendJoinInfo(string controllerId)
    {
        // Announce presence so the handshake completes and the P2P link is proven.
        var joinInfo = new NetworkMissionJoinInfo(
            Session.OwnControllerId,
            isPlayerAlive: true,
            aiAgentDatas: Array.Empty<CoopAgentSpawnData>());

        network.Send(controllerId, joinInfo);
        Logger.Information("[Session] Sent join info to {Controller} for instance {Instance}", controllerId, Session.InstanceId);

        void ReplayJoinState()
        {
            replicator.ReplicateCurrentAgentsTo(controllerId);
            siegeEngineDeployment.CatchUpJoiner(controllerId);
            siegeMachineState.CatchUpJoiner(controllerId);
            if (Session.IsLocalHost)
                coopMissionComponent.WeaponDropHandler.CatchUpJoiner(controllerId);

            messageBroker.Publish(this, new SessionMemberJoined(Session.InstanceId, controllerId));
        }

        // A timed-out blocking run never starts later, so the catch queues the replay again. The shared claim
        // keeps it to one run when the first copy had already started.
        int replayClaimed = 0;
        void ReplayJoinStateOnce()
        {
            if (System.Threading.Interlocked.Exchange(ref replayClaimed, 1) == 0)
                ReplayJoinState();
        }

        try
        {
            GameThread.RunSafe(
                ReplayJoinStateOnce,
                blocking: true,
                context: nameof(SendJoinInfo));
        }
        catch (TimeoutException e)
        {
            GameThread.RunSafe(ReplayJoinStateOnce, context: nameof(SendJoinInfo));
            Logger.Warning(
                e,
                "[Session] Join replay barrier for {Controller} timed out; the replay is queued again",
                controllerId);
        }
        catch (OperationCanceledException e)
        {
            Logger.Warning(
                e,
                "[Session] Join replay barrier for {Controller} was canceled; the replay is not queued again",
                controllerId);
        }
    }

    protected override void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo)
    {
        Logger.Information("[Session] Received join info from {Controller} (instance {Instance})", joinInfo.ControllerId, Session.InstanceId);
    }

    private void Handle_BattleHostAssigned(MessagePayload<BattleHostAssignmentApplied> payload)
    {
        var assignment = payload.What.Assignment;
        if (assignment.MapEventId != Session.InstanceId)
            return;

        GameThread.RunSafe(() =>
        {
            // A successor-line change re-sends the same host and epoch.
            if (assignment.HostControllerId == lastHost && assignment.Epoch == lastEpoch)
                return;

            lastHost = assignment.HostControllerId;
            lastEpoch = assignment.Epoch;
            messageBroker.Publish(this, new SessionHostChanged(
                assignment.MapEventId,
                assignment.HostControllerId,
                assignment.Epoch,
                Session.IsLocalHost));
        }, context: nameof(Handle_BattleHostAssigned));
    }

    private void Handle_EndMission(MessagePayload<NetworkEndBattleMission> payload)
    {
        var sessionId = payload.What.MapEventId;
        if (sessionId != Session.InstanceId)
            return;

        GameThread.RunSafe(() => messageBroker.Publish(this, new SessionMissionEndRequested(sessionId)),
            context: nameof(Handle_EndMission));
    }

    /// <summary>Asks the server to close the session. Only the current host may; the server checks it again.</summary>
    internal bool TryRequestClose()
    {
        if (!Session.IsLocalHost)
            return false;

        relayNetwork.SendAll(new NetworkRequestSessionClose(Session.InstanceId, Session.HostEpoch));
        return true;
    }

    protected override void OnLeaving()
    {
        damageRouter.FlushForMissionEnd();
        lifecycle.Leave(wasRetreat: false);
    }

    protected override void DisposeMission()
    {
        Cleanup(
            lifecycle.Dispose,
            replicator.Dispose,
            deathReporter.Dispose,
            routReporter.Dispose,
            puppetSpawner.Dispose,
            puppetDeathApplier.Dispose,
            puppetRoutApplier.Dispose,
            damageRouter.Dispose,
            authorityMigrator.Dispose,
            reinforcementFielder.Dispose,
            siegeEngineDeployment.Dispose,
            () => coopMissionComponent.AgentActionHandler.BindPilotSeats(null, null),
            siegeMachineState.Dispose,
            siegeWeaponFire.Dispose,
            deployment.Dispose,
            () => messageBroker.Unsubscribe<BattleHostAssignmentApplied>(Handle_BattleHostAssigned),
            () => messageBroker.Unsubscribe<NetworkEndBattleMission>(Handle_EndMission),
            () =>
            {
                SiegeMissionAuthorityGate.IsLocalAuthority = false;
                SiegeMissionAuthorityGate.IsAuthorityKnown = false;
                SiegeMissionAuthorityGate.ResetClaimedMachines();
            },
            base.DisposeMission);
    }
}
