using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Missions.Data;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Per-mission P2P controller for a field battle — the battle counterpart to
/// <see cref="Taverns.CoopLocationsController"/>. Attached to a freshly opened battle mission by
/// <c>BattleMissionEntryPatch</c>. On entry it connects to the mission-scoped mesh instance keyed by the
/// map event's object-manager id, so every player fighting the same battle joins the same P2P instance,
/// then exchanges join info over the mesh.
/// <para>
/// Phase 0 scope: establish the P2P instance and prove the join-info handshake. Authoritative troop
/// spawning and per-agent sync are layered on in later phases (see <c>doc</c> / the dormant
/// <c>Missions.Agents</c> handlers).
/// </para>
/// </summary>
public class CoopBattleController : CoopMissionController, IBattleMissionBehavior, IBattleDeploymentBridge, IBattleDeploymentRevealSink
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleController>();

    private readonly INetwork relayNetwork;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IBattleHostRegistry hostRegistry;

    private string instanceId;
    private bool _instanceRequested;

    // Deployment activation: gates NPC movement on the first deployment-finished from ANY client (the "any
    // client" rule of requirement #2). Pure decision logic; this controller is its bridge (see the explicit
    // IBattleDeploymentBridge members below) so the activator itself stays mission/network-free and unit-tested.
    private readonly IBattleDeploymentActivator _activator;

    // Requirement #4 "hidden everywhere until deployed": decides whether a freshly-captured own-party spawn is
    // withheld from peers (while we are still placing our formations) or replicated, and on our commit reveals
    // the withheld troops at their deployed positions. Pure decision logic; this controller is its reveal sink.
    private readonly BattleDeploymentRevealGate _revealGate;

    // Casualty attribution per agent (map-event party id + troop descriptor seed), captured at spawn and used
    // by the agent's owner to report its death to the server. Written from the main thread (host capture) and
    // the network thread (peer puppet spawn), read on death — hence concurrent.
    // Per-agent casualty attribution captured at spawn. troopSeed feeds the puppet origin on replay; the
    // troopCharacterId is what the server keys the roster casualty on (descriptor seeds churn — see
    // NetworkRequestBattleCasualty).
    private readonly ConcurrentDictionary<Guid, (string mapEventPartyId, int troopSeed, string troopCharacterId)> _casualtyInfo = new();

    // Throttled supply-progress reporting (game thread, via OnMissionTick): tells the server how far each of
    // our troop suppliers has spawned, so its ledger pointer advances and a new owner can resume from it on
    // disconnect/migration. Only sends when a count changed.
    private const float SupplyReportInterval = 1f;
    private float _supplyReportTimer;
    private readonly Dictionary<string, int> _lastSupplyReport = new();

    // Puppet spawns from the host's catch-up burst can arrive while THIS client's mission is still loading
    // (before MissionCombatantsLogic creates the teams). An agent built with a null team later NREs the
    // scoreboard (BattleObserverMissionLogic.SetObserver reads agent.Team.Side from its build cache), so buffer
    // such spawns and drain them on tick once the teams exist.
    private readonly object _pendingPuppetLock = new object();
    private readonly List<BattleAgentSpawnData> _pendingPuppets = new List<BattleAgentSpawnData>();

    // [Host] Map-event party ids we have already fielded as mid-battle reinforcements, so a repeated involved-
    // parties broadcast for the same party doesn't double-spawn it.
    private readonly HashSet<string> _reinforcedParties = new HashSet<string>();

    public CoopBattleController(
        IBattleNetwork network,
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleHostRegistry hostRegistry)
        : base(network, messageBroker, objectManager, coopMissionComponent)
    {
        this.relayNetwork = relayNetwork;
        this.controllerIdProvider = controllerIdProvider;
        this.hostRegistry = hostRegistry;

        messageBroker.Subscribe<NetworkMissionLeft>(Handle_LeaveMission);
        messageBroker.Subscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);

        // Phase 2 host-authoritative spawn: the host captures each spawned agent and replicates it; peers
        // receive those and spawn puppets.
        messageBroker.Subscribe<AgentSpawnedInBattle>(Handle_AgentSpawnedInBattle);
        messageBroker.Subscribe<NetworkSpawnBattleAgents>(Handle_NetworkSpawnBattleAgents);

        // Phase 3 death replication: the agent's owner reports its death; every client kills its puppet.
        messageBroker.Subscribe<BattleAgentDied>(Handle_BattleAgentDied);
        messageBroker.Subscribe<NetworkBattleAgentDied>(Handle_NetworkBattleAgentDied);

        // Combat damage routing: a local troop hitting a puppet is suppressed locally and routed to the
        // puppet's owner, which applies it authoritatively — so each agent's life/death is decided on exactly
        // one client and the battles don't diverge.
        messageBroker.Subscribe<BattlePuppetHit>(Handle_BattlePuppetHit);
        messageBroker.Subscribe<NetworkApplyBattleDamage>(Handle_NetworkApplyBattleDamage);

        // A player leaving/dropping: the host adopts their troops (authority + AI control) so they keep
        // fighting instead of vanishing. Server-mediated, so it fires even on a silent P2P drop.
        messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);

        // Host migration: when the previous host departs and the server promotes us, adopt its orphaned
        // agents (the AI/enemy it was running, plus its own troops).
        messageBroker.Subscribe<BattleHostMigrated>(Handle_BattleHostMigrated);

        // Deployment activation: clients announce when they finish deploying; the host releases the NPC AI on
        // the first announcement from any client and broadcasts that the battle is live (so a migrated host
        // knows to release the NPCs it adopts). This controller is the activator's bridge to the mesh + mission.
        _activator = new BattleDeploymentActivator(this);
        _revealGate = new BattleDeploymentRevealGate(this);
        messageBroker.Subscribe<NetworkBattleDeploymentFinished>(Handle_NetworkBattleDeploymentFinished);
        messageBroker.Subscribe<NetworkBattleActivated>(Handle_NetworkBattleActivated);

        // [Host] A new AI party joining the live battle is fielded through our own spawn path (reinforcements).
        messageBroker.Subscribe<NetworkAddInvolvedParties>(Handle_ReinforcementPartiesAdded);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMissionLeft>(Handle_LeaveMission);
        messageBroker.Unsubscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Unsubscribe<AgentSpawnedInBattle>(Handle_AgentSpawnedInBattle);
        messageBroker.Unsubscribe<NetworkSpawnBattleAgents>(Handle_NetworkSpawnBattleAgents);
        messageBroker.Unsubscribe<BattleAgentDied>(Handle_BattleAgentDied);
        messageBroker.Unsubscribe<NetworkBattleAgentDied>(Handle_NetworkBattleAgentDied);
        messageBroker.Unsubscribe<BattlePuppetHit>(Handle_BattlePuppetHit);
        messageBroker.Unsubscribe<NetworkApplyBattleDamage>(Handle_NetworkApplyBattleDamage);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
        messageBroker.Unsubscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
        messageBroker.Unsubscribe<NetworkBattleDeploymentFinished>(Handle_NetworkBattleDeploymentFinished);
        messageBroker.Unsubscribe<NetworkBattleActivated>(Handle_NetworkBattleActivated);
        messageBroker.Unsubscribe<NetworkAddInvolvedParties>(Handle_ReinforcementPartiesAdded);

        base.Dispose();
    }

    // The battle mission was opened locally and this controller attached by BattleMissionEntryPatch before
    // the event was published, so it is the live, mission-scoped owner of the P2P connection. The instance
    // id is the map event's object-manager id — identical on every client in the battle, so the server
    // creates the instance on the first NAT punch and no assignment round-trip is needed.
    private void Handle_PlayerEnteredBattle(MessagePayload<PlayerEnteredBattle> payload)
    {
        // OpenBattleMission can fire more than once around an encounter; connect once per mission.
        if (_instanceRequested) return;

        var mapEvent = payload.What.MapEvent;
        if (mapEvent == null)
        {
            Logger.Warning("[BattleSync] PlayerEnteredBattle with no map event — skipping instance request");
            return;
        }

        if (objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId) == false)
        {
            Logger.Warning("[BattleSync] Could not resolve map event id — skipping instance request");
            return;
        }

        _instanceRequested = true;
        instanceId = mapEventId;

        // The spawn gate is engaged earlier, in BattleMissionEntryPatch's prefix (before the mission's troops
        // spawn). This handler only owns the P2P instance connect + host-election request.

        Logger.Information("[BattleSync] Requesting P2P battle instance mapEvent={MapEventId}", mapEventId);

        network.Start();
        network.ConnectToInstance(instanceId);
        coopMissionComponent.AgentRegistry.Clear();

        relayNetwork.SendAll(new NetworkMissionEntered(controllerIdProvider.ControllerId, instanceId));
        Logger.Information("[Relay] Announced MissionEntered for battle instance {Instance}", instanceId);
    }

    protected override void SendJoinInfo(string controllerId)
    {
        // Announce presence so the handshake completes and the P2P link is proven.
        var joinInfo = new NetworkMissionJoinInfo(
            controllerIdProvider.ControllerId,
            isPlayerAlive: true,
            aiAgentDatas: Array.Empty<CoopAgentSpawnData>());

        network.Send(controllerId, joinInfo);
        Logger.Information("[BattleSync] Sent join info to {Controller} for instance {Instance}", controllerId, instanceId);

        // Catch a mid-battle joiner up on the ACTIVATION state. NetworkBattleActivated is a one-shot broadcast
        // sent when the battle went live, so a client that joins afterward never heard it and would otherwise sit
        // at activated=false — its puppets stay frozen through its own deployment (they aren't un-paused; see
        // SpawnPuppet) and a later promotion to host wouldn't release the NPCs it adopts (OnPromotedToHost gates
        // on activation). Any already-activated peer re-tells the joiner; OnBattleActivatedReceived is idempotent.
        if (_activator.IsActivated)
        {
            network.Send(controllerId, new NetworkBattleActivated(instanceId));
            Logger.Information("[BattleSync] Told joining {Controller} the battle is already activated", controllerId);
        }

        // Catch the joiner up to the live battle. The per-spawn broadcasts (Handle_AgentSpawnedInBattle) only
        // reached peers already connected when each agent spawned, so a player entering after troops are on
        // the field — including a mid-battle joiner — would otherwise miss them. Each client replays the
        // agents IT owns, so the joiner is caught up from every owner (its own troops it spawns natively).
        ReplicateCurrentAgentsTo(controllerId);
    }

    // Replay the battle agents WE own to a peer that just joined, so it spawns matching puppets driven by our
    // movement. Reading agent transforms must run on the game thread; the send is inside the same action so
    // the snapshot and the message stay consistent. Delivery is reliable (MessagePacket -> ReliableOrdered),
    // so the whole batch fragments and arrives intact.
    private void ReplicateCurrentAgentsTo(string controllerId)
    {
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            // A joiner catches up on everything we own (our own party AND, on the host, the AI it drives).
            var records = BuildOwnedAgentRecords(ownPartyOnly: false);
            if (records.Count == 0) return;

            network.Send(controllerId, new NetworkSpawnBattleAgents(records.ToArray()));
            Logger.Information("[BattleSync] Replayed {Count} of our agent(s) to joining {Controller}", records.Count, controllerId);
        });
    }

    // [Game thread] Build spawn records for the battle agents WE currently own, at their CURRENT positions.
    // <paramref name="ownPartyOnly"/> limits it to the local player's own-party troops — used by the deployment
    // commit, which withholds those until they are placed; the joiner catch-up passes false to replay all we own.
    private List<BattleAgentSpawnData> BuildOwnedAgentRecords(bool ownPartyOnly)
    {
        var records = new List<BattleAgentSpawnData>();
        foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(controllerIdProvider.ControllerId))
        {
            var agent = info.Agent;
            if (agent == null || !agent.IsActive() || !(agent.Character is CharacterObject character)) continue;
            if (ownPartyOnly && !IsOwnPartyAgent(agent, character)) continue;

            // Carried by CharacterObject id, uniform for heroes and troops (hero CharacterObjects are registered).
            if (!objectManager.TryGetId(character, out var characterId)) continue;

            _casualtyInfo.TryGetValue(info.AgentId, out var attribution);
            var side = agent.Team != null ? agent.Team.Side : BattleSideEnum.None;

            records.Add(new BattleAgentSpawnData(
                info.AgentId, characterId, agent.Position, side, agent.Health,
                controllerIdProvider.ControllerId, attribution.mapEventPartyId, attribution.troopSeed));
        }
        return records;
    }

    // Whether an agent belongs to the LOCAL player's own party — the troops withheld until deployment commit
    // (requirement #4). The player's hero and the troops the local supplier spawned for MainParty are own-party;
    // the host's enemy/allied AI (a different origin party) is not, so it shows up frozen during deployment (#1).
    private static bool IsOwnPartyAgent(Agent agent, CharacterObject character)
    {
        if (character.IsHero && character.HeroObject == Hero.MainHero) return true;
        return agent.Origin is CoopAgentOrigin origin && origin.Party == PartyBase.MainParty;
    }

    // [Owner, game thread] On committing deployment, replicate our own-party troops at their DEPLOYED positions so
    // peers spawn matching puppets where we placed them. Until commit these were withheld (requirement #4: hidden
    // everywhere until deployed). Called from OnDeploymentFinished — already on the game thread and before the
    // native un-pause moves the troops — so the captured positions are the deployed ones.
    private void BroadcastOwnDeployedTroops()
    {
        if (Mission.Current == null) return;

        var records = BuildOwnedAgentRecords(ownPartyOnly: true);
        if (records.Count == 0) return;

        network.SendAll(new NetworkSpawnBattleAgents(records.ToArray()));
        Logger.Information("[BattleSync] Committed deployment: broadcast {Count} own-party troop(s) at deployed positions", records.Count);
    }

    protected override void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo)
    {
        // Phase 0: log receipt to confirm the bidirectional handshake. Spawning the peer's troops is a
        // later phase (see CoopLocationsController.HandleJoinInfo for the spawn+register pattern).
        Logger.Information("[BattleSync] Received join info from {Controller} (instance {Instance})", joinInfo.ControllerId, instanceId);
    }

    protected override void OnLeaving()
    {
        // Commit the concluded battle's result to the campaign BEFORE tearing the instance down, so the server
        // captures losers / awards the win and finalizes the encounter. A live coop battle never sets the map
        // event's BattleState on its own (the encounter doesn't resolve), so without this the defeated players
        // are left uncaptured with the encounter still open.
        CommitBattleResultIfHost();

        BattleSpawnGate.EndBattle();

        if (instanceId != null)
        {
            CoopTroopSupplierRegistry.ClearBattle(instanceId);
            relayNetwork.SendAll(new NetworkMissionLeft(controllerIdProvider.ControllerId, instanceId));
            Logger.Information("[Relay] Announced MissionLeft for battle instance {Instance}", instanceId);
        }

        network.Stop();
    }

    /// <summary>
    /// [Host] Commits this concluded coop battle's <see cref="MissionResult"/> to the campaign map event. Setting
    /// <c>MapEvent.BattleState</c> runs the native setter, which the coop intercept (<c>MapEventPatches</c>) syncs
    /// to the server — there it runs <c>OnBattleWon</c> (capturing the defeated players) and the auto-finalize
    /// (closing every player's encounter). Only the host commits; a retreat (unresolved result) commits nothing;
    /// an already-resolved state (e.g. a simulated battle) is left untouched.
    /// </summary>
    private void CommitBattleResultIfHost()
    {
        if (instanceId == null || !hostRegistry.IsLocalHost(instanceId)) return;

        var result = Mission.Current?.MissionResult;
        if (result == null || !result.BattleResolved) return;

        if (!objectManager.TryGetObject<MapEvent>(instanceId, out var mapEvent)) return;
        if (mapEvent.BattleState != BattleState.None) return;

        Logger.Information("[BattleSync] Committing concluded battle result {State} for instance {Instance}", result.BattleState, instanceId);
        mapEvent.BattleState = result.BattleState;
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        DrainPendingPuppets();
        LogTeamDiagnosticOnce(dt);

        _supplyReportTimer += dt;
        if (_supplyReportTimer < SupplyReportInterval) return;
        _supplyReportTimer = 0f;
        ReportSupplyProgress();
    }

    // [All clients] The local player just finished their own deployment (Start Battle): the native
    // FinishDeployment un-paused our own troops and handed us our hero. Announce it to the battle mesh so the
    // host releases the NPC AI on the first finish from ANY client. If WE are the host, that same native
    // FinishDeployment also released our NPCs, so record the battle as activated to ignore later finishes.
    public override void OnDeploymentFinished()
    {
        base.OnDeploymentFinished();

        // We finished our own deployment (Start Battle): the native FinishDeployment un-paused our own troops
        // and handed us our hero. The activator announces it to peers and — if we are the host — records the
        // battle as live (that same FinishDeployment released our NPCs) and broadcasts it.
        _activator.OnLocalDeploymentFinished();

        // Reveal the troops we just placed: marks deployment committed (so own-party reinforcements now replicate
        // immediately) and broadcasts the withheld own-party troops at their deployed positions. We are on the
        // game thread and the un-pause has not run yet, so those are the deployed positions.
        _revealGate.OnDeploymentCommitted();

        Logger.Information("[BattleSync] Local deployment finished (host={IsHost}, activated={Active})",
            hostRegistry.IsLocalHost(instanceId), _activator.IsActivated);
    }

    // [Host] A peer finished deploying before we did. Release the NPC AI now so it engages while we (and any
    // other players) are still placing our own formations — the "any client" gate. Our own troops stay frozen
    // until our own Start Battle. Non-hosts drive no NPCs (theirs are puppets that follow us), so they ignore
    // this; and once we have already activated (our own finish, or an earlier peer), later finishes are no-ops.
    private void Handle_NetworkBattleDeploymentFinished(MessagePayload<NetworkBattleDeploymentFinished> payload)
    {
        // Host-only + first-finish gating lives in the activator; on the host's first remote finish it releases
        // the NPC AI (we are still deploying) and broadcasts the battle-activated signal.
        Logger.Information("[BattleSync] Peer {Controller} finished deployment", payload.What.ControllerId);
        _activator.OnRemoteDeploymentFinished();
    }

    // The host announced the battle is live (NPCs released). Record it so a later promotion to host (migration)
    // releases the NPCs we adopt even while we are still in our own deployment. Non-hosts otherwise need no
    // action — their NPCs are host-driven puppets that follow the host's movement.
    private void Handle_NetworkBattleActivated(MessagePayload<NetworkBattleActivated> payload)
    {
        Logger.Information("[BattleSync] Battle-activated signal received for {Instance}", payload.What.MapEventId);
        _activator.OnBattleActivatedReceived();
    }

    // --- IBattleDeploymentBridge: the mission + mesh effects the BattleDeploymentActivator drives ---
    bool IBattleDeploymentBridge.IsLocalHost => hostRegistry.IsLocalHost(instanceId);
    void IBattleDeploymentBridge.AnnounceLocalDeploymentFinished() => network.SendAll(new NetworkBattleDeploymentFinished(controllerIdProvider.ControllerId));
    void IBattleDeploymentBridge.BroadcastBattleActivated() => network.SendAll(new NetworkBattleActivated(instanceId));
    void IBattleDeploymentBridge.ReleaseNpcAi() => ActivateNpcAi();

    // IBattleDeploymentRevealSink: the BattleDeploymentRevealGate's commit effect (replicate own-party troops at
    // their deployed positions, requirement #4).
    void IBattleDeploymentRevealSink.RevealOwnTroopsAtDeployedPositions() => BroadcastOwnDeployedTroops();

    // [Host, game thread] Release the host-driven NPC AI so it engages mid-deployment. The mission gates AI
    // globally during deployment (Mission.AllowAiTicking == false) and the agents are AI-paused; turn ticking
    // back on and un-pause the enemy side exactly as the native FinishDeployment does per agent. The host's OWN
    // deploying troops stay put because they remain AI-paused until the host's own Start Battle.
    // Phase B scope: the enemy side (the NPCs the host owns and drives). Releasing allied AI on the host's own
    // side, while excluding the host's own still-deploying party, needs the per-party ownership info that comes
    // with the deployment-authority work (requirement #3/#4) — until then those release on the host's own finish.
    private void ActivateNpcAi()
    {
        GameThread.RunSafe(() =>
        {
            var mission = Mission.Current;
            if (mission == null) return;

            var hostSide = PartyBase.MainParty?.Side ?? BattleSideEnum.None;
            var enemySide = hostSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;

            // Global AI gate back on so the enemy formations tick; our own side stays put because it is AI-paused.
            mission.AllowAiTicking = true;

            int released = 0;
            foreach (var team in mission.Teams)
            {
                if (team.Side != enemySide) continue;

                foreach (var formation in team.FormationsIncludingSpecialAndEmpty)
                {
                    if (formation.CountOfUnits <= 0) continue;

                    formation.SetControlledByAI(true);
                    formation.ApplyActionOnEachUnit(agent =>
                    {
                        if (!agent.IsAIControlled) return;
                        agent.SetAlarmState(Agent.AIStateFlag.Alarmed);
                        agent.SetIsAIPaused(false);
                        if (agent.GetAgentFlags().HasFlag(AgentFlag.CanWieldWeapon))
                            agent.ResetEnemyCaches();
                        agent.HumanAIComponent?.SyncBehaviorParamsIfNecessary();
                        released++;
                    });
                }

                team.QuerySystem.Expire();
                team.ResetTactic();
            }

            Logger.Information("[BattleSync] Released {Count} enemy NPC agent(s) on first deployment finish", released);
        });
    }

    // TEMP diagnostic: a few seconds in (after spawns settle), dump the team/agent/player state once so we can
    // see why the player's side may be absent (no Defender team? team but no agents? hero spawned but not the
    // MainAgent?). Remove once the spawn/attach is solid.
    private float _diagTimer;
    private bool _diagLogged;
    private void LogTeamDiagnosticOnce(float dt)
    {
        if (_diagLogged) return;
        _diagTimer += dt;
        if (_diagTimer < 4f) return;
        _diagLogged = true;

        var mission = Mission.Current;
        if (mission == null) return;

        foreach (var team in mission.Teams)
        {
            // List the HERO agents on each team — pinpoints where the player's own hero, the host hero and the
            // AI-lord heroes actually land (PlayerTeam = controllable by us; ally team = not).
            var heroes = new List<string>();
            foreach (var agent in team.ActiveAgents)
                if (agent.Character != null && agent.Character.IsHero)
                    heroes.Add(agent.Character.StringId);

            Logger.Information("[BattleDiag] Team side={Side} isPlayerTeam={IsPlayer} isPlayerAlly={IsAlly} activeAgents={Count} heroes=[{Heroes}]",
                team.Side, team == mission.PlayerTeam, team == mission.PlayerAllyTeam, team.ActiveAgents.Count, string.Join(", ", heroes));
        }

        var heroChar = Hero.MainHero?.CharacterObject;
        bool heroOnField = false;
        foreach (var agent in mission.Agents)
            if (agent.Character == heroChar)
            {
                heroOnField = true;
                Logger.Information("[BattleDiag] Player hero {Char} is on team side={Side} isPlayerTeam={IsPlayer} (controller={Ctrl})",
                    heroChar.StringId, agent.Team?.Side, agent.Team == mission.PlayerTeam, agent.Controller);
                break;
            }

        var spawnLogic = mission.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();
        Logger.Information("[BattleDiag] AttackerTeam={Atk} DefenderTeam={Def} PlayerTeam={Player} MainAgent={Main} heroOnField={Hero} spawnEnabled(Def={SDef},Atk={SAtk}) playerSide={PSide}",
            mission.AttackerTeam != null, mission.DefenderTeam != null,
            mission.PlayerTeam?.Side.ToString() ?? "null",
            mission.MainAgent != null,
            heroOnField,
            spawnLogic?.IsSideSpawnEnabled(BattleSideEnum.Defender),
            spawnLogic?.IsSideSpawnEnabled(BattleSideEnum.Attacker),
            PartyBase.MainParty?.Side);
    }

    // [Owner, game thread] Report how far each of our suppliers has spawned so the server's ledger pointer
    // advances; a new owner is then resumed from it on disconnect/migration. Only owned parties have entries
    // (a non-owned side's supplier is empty), and we skip the send when nothing changed.
    private void ReportSupplyProgress()
    {
        if (instanceId == null) return;

        var entries = new List<SupplyProgressEntry>();
        bool changed = false;
        foreach (var supplier in CoopTroopSupplierRegistry.GetSuppliers(instanceId))
        {
            foreach (var (partyId, supplied) in supplier.GetSuppliedByParty())
            {
                entries.Add(new SupplyProgressEntry(partyId, supplied));
                if (!_lastSupplyReport.TryGetValue(partyId, out var last) || last != supplied)
                {
                    changed = true;
                    _lastSupplyReport[partyId] = supplied;
                }
            }
        }

        if (!changed || entries.Count == 0) return;
        relayNetwork.SendAll(new NetworkBattleSupplyProgress(instanceId, entries.ToArray()));
    }

    private void Handle_LeaveMission(MessagePayload<NetworkMissionLeft> payload)
    {
        // Our own broadcast echoed back by a peer — ignore. Phase 0 has no remote agents to release; later
        // phases despawn the leaver's troops here (see CoopLocationsController.Handle_LeaveMission).
        if (payload.What.ControllerId == controllerIdProvider.ControllerId) return;

        Logger.Information("[BattleSync] Peer {ControllerId} left battle instance {Instance}", payload.What.ControllerId, instanceId);
    }

    // [Owner] An agent WE spawned into the battle was captured (BattleAgentSpawnedPatch). Each client spawns
    // only the troops it owns — its own party, plus the AI/enemy side on the host — so we are this agent's
    // owner: give it a network id, register it under us, and replicate it so peers spawn a matching puppet
    // driven by our movement. Our own hero is already the native main agent, so no adoption is needed here.
    private void Handle_AgentSpawnedInBattle(MessagePayload<AgentSpawnedInBattle> payload)
    {
        var agent = payload.What.Agent;
        if (agent == null || !(agent.Character is CharacterObject character)) return;

        AttachPlayerAgent(agent, character);

        // Carried by CharacterObject id, uniform for heroes and troops (hero CharacterObjects are registered).
        if (!objectManager.TryGetId(character, out var characterId)) return;

        string owner = controllerIdProvider.ControllerId;
        var agentId = Guid.NewGuid();
        coopMissionComponent.AgentRegistry.TryRegisterAgent(owner, agentId, agent);

        // Casualty attribution: the battle-troop origin carries the map-event party and the exact troop
        // descriptor seed the server's OnTroopKilled path keys on. Carry them so we can report the casualty
        // on death (puppets, spawned with a SimpleAgentOrigin, get these from the spawn data).
        string mapEventPartyId = null;
        int troopSeed = 0;
        // Our coop spawns carry a CoopAgentOrigin (the custom supplier's origin), NOT the native
        // PartyGroupAgentOrigin — read the party + descriptor seed from it. Checking for the native type here
        // left attribution null, so the death report was skipped and the map-event roster never decremented.
        if (agent.Origin is CoopAgentOrigin origin && origin.Party != null)
        {
            troopSeed = origin.UniqueSeed;
            var mapEventParty = ResolveMapEventParty(origin.Party);
            if (mapEventParty != null && objectManager.TryGetId(mapEventParty, out var mepId))
                mapEventPartyId = mepId;
        }
        // The casualty keys on the troop's CHARACTER — exactly `characterId`, the CharacterObject's object-manager
        // id we also carry in the spawn data.
        _casualtyInfo[agentId] = (mapEventPartyId, troopSeed, characterId);

        BattleSideEnum side = agent.Team != null ? agent.Team.Side : BattleSideEnum.None;
        var data = new BattleAgentSpawnData(agentId, characterId, agent.Position, side, agent.Health, owner, mapEventPartyId, troopSeed);

        // Requirement #4 "hidden everywhere until deployed": while we are still placing our own formations our
        // own-party troops are spawned locally (so we can deploy them) but NOT replicated, so other clients never
        // see them mid-deployment. They are broadcast at their deployed positions when we commit (see
        // OnDeploymentFinished -> BroadcastOwnDeployedTroops). NPC/AI agents WE own (the host's enemy side) are
        // not withheld — they must show up frozen on every client during deployment (requirement #1).
        if (_revealGate.ShouldWithhold(IsOwnPartyAgent(agent, character)))
        {
            Logger.Information("[BattleSync] Withholding own spawn {Char} (agent {AgentId}) until deployment commit", characterId, agentId);
            return;
        }

        // SendAll over the mesh reaches every peer in this battle instance (not us).
        Logger.Information("[BattleSync] Captured own spawn {Char} (agent {AgentId}); broadcasting over mesh", characterId, agentId);
        network.SendAll(new NetworkSpawnBattleAgents(new[] { data }));
    }

    private void AttachPlayerAgent(Agent agent, CharacterObject character)
    {
        // While a deployment phase is live, the deployment controller owns the player agent — it holds our hero
        // as Controller.None until Start Battle and assigns Mission.MainAgent itself on FinishDeployment (via
        // AssignPlayerRoleInTeamMissionController). Forcing Player control here would fight that freeze, so defer
        // to native deployment; this attach only matters post-deployment (e.g. adopting our own hero puppet).
        if (Mission.Current?.GetMissionBehavior<DeploymentMissionController>() != null)
            return;

        // Our own hero just spawned: take control of it ourselves — set it as the mission's controllable main
        // agent (the camera follows the main agent). Done before the resolution/registration below so it runs
        // even if those fail.
        if (character.IsHero && character.HeroObject == Hero.MainHero && Mission.Current != null && Mission.Current.MainAgent != agent)
        {
            agent.Controller = AgentControllerType.Player;
            Mission.Current.MainAgent = agent;
            Logger.Information("[BattleSync] Attached player to own hero agent ({Char})", character.StringId);
        }
    }

    // [Peer] Spawn the host's agents as local puppets. They are inert in v1 (no AI/movement); Phase 3 drives
    // them from replicated movement/combat and adopts the local player's own hero as the controllable agent.
    private void Handle_NetworkSpawnBattleAgents(MessagePayload<NetworkSpawnBattleAgents> payload)
    {
        if (payload.What.Agents == null) return;

        Logger.Information("[BattleSync] Received {Count} spawn record(s) from the host over the mesh", payload.What.Agents.Length);
        foreach (var data in payload.What.Agents)
            SpawnPuppet(data);
    }

    // [Owner] One of our authoritative agents died. Tell every client to kill its puppet of it, then drop it
    // from the registry so the movement handler stops broadcasting it. A puppet death (from an applied
    // broadcast) is ignored here — its authority is the owner, not us — so there is no echo.
    private void Handle_BattleAgentDied(MessagePayload<BattleAgentDied> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(payload.What.Agent, out var info))
        {
            Logger.Information("[DeathDiag] An agent died but is not in our registry — not ours to broadcast (a puppet or an uncaptured agent)");
            return;
        }
        if (info.CurrentAuthority != controllerIdProvider.ControllerId)
        {
            Logger.Information("[DeathDiag] Agent {AgentId} died but its authority is {Auth}, not us ({Us}) — not broadcasting", info.AgentId, info.CurrentAuthority, controllerIdProvider.ControllerId);
            return;
        }

        _casualtyInfo.TryGetValue(info.AgentId, out var attribution);

        // Only wound player heros
        bool wounded = payload.What.Wounded;
        if (attribution.troopCharacterId != null
            && objectManager.TryGetObject<CharacterObject>(attribution.troopCharacterId, out var troop)
            && troop.HeroObject?.IsPlayerHero() == true)
        {
            wounded = true;
        }

        Logger.Information("[DeathDiag] Broadcasting death of agent {AgentId} (wounded={Wounded}) to the battle mesh", info.AgentId, wounded);
        network.SendAll(new NetworkBattleAgentDied(info.AgentId, wounded));

        // Owner-authoritative casualty: tell the server to account this troop's death/wound against its
        // map-event party roster. The host's own mission accounting is suppressed during a coop battle
        // (MapEventPartyPatches), so this is the single source. On a client, SendAll targets the server.
        if (attribution.mapEventPartyId != null)
            relayNetwork.SendAll(new NetworkRequestBattleCasualty(attribution.mapEventPartyId, attribution.troopCharacterId, wounded));

        _casualtyInfo.TryRemove(info.AgentId, out _);
        registry.RemoveAgent(info.AgentId);
    }

    // [Peer] The owner reported one of its agents died — kill our puppet of it and deregister.
    private void Handle_NetworkBattleAgentDied(MessagePayload<NetworkBattleAgentDied> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;
        Logger.Information("[DeathDiag] Received death broadcast for agent {AgentId}", payload.What.AgentId);
        if (!registry.TryGetAgentInfo(payload.What.AgentId, out _))
        {
            Logger.Information("[DeathDiag] No registered puppet for {AgentId} — cannot kill it (its spawn was missed, or the id does not match)", payload.What.AgentId);
            return;
        }

        GameThread.RunSafe(() =>
        {
            if (!registry.TryGetAgentInfo(payload.What.AgentId, out var info)) return;

            Agent agent = info.Agent;
            if (Mission.Current == null) return;
            Logger.Information("[DeathDiag] Killing puppet {AgentId}: agentPresent={Present}, health={Health}", payload.What.AgentId, agent != null, agent?.Health ?? -1f);
            if (agent != null && agent.Health > 0)
            {
                agent.MakeDead(false, ActionIndexCache.act_none);
                agent.FadeOut(false, true);
            }

            // Deregister AFTER the kill, INSIDE this game-thread action. We receive this on the network thread,
            // so RunSafe queues the kill; removing on the network thread (outside the lambda) raced ahead of it,
            // and the re-check above then found the agent already gone and bailed — so MakeDead never ran and the
            // puppet stayed alive (removed but not killed → an invincible, unroutable agent).
            registry.RemoveAgent(payload.What.AgentId);
            _casualtyInfo.TryRemove(payload.What.AgentId, out _);
        });
    }

    // [Attacker's node] A local troop hit a puppet (suppressed locally by BattleBlowInterceptPatch). Route the
    // WHOLE blow to the puppet's owner; only the owner re-applies it. The attacker's network id rides along so
    // the owner can re-map the (per-client) attacker index to its local agent.
    private void Handle_BattlePuppetHit(MessagePayload<BattlePuppetHit> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(payload.What.Victim, out var victimInfo))
        {
            Logger.Information("[DeathDiag] Local hit on a puppet that is not in our registry — cannot route it");
            return;
        }

        Guid attackerId = Guid.Empty;
        if (payload.What.Attacker != null && registry.TryGetAgentInfo(payload.What.Attacker, out var attackerInfo))
            attackerId = attackerInfo.AgentId;

        Logger.Information("[DeathDiag] Routing puppet hit to owner {Owner}: victim={Victim}, dmg={Dmg}", victimInfo.CurrentAuthority, victimInfo.AgentId, payload.What.Blow.InflictedDamage);
        network.SendAll(new NetworkApplyBattleDamage(victimInfo.AgentId, attackerId, payload.What.Blow, payload.What.CollisionData));
    }

    // [Owner] Another client's troop hit one of OUR agents. Re-apply the real blow through Agent.RegisterBlow so
    // the engine resolves damage, hit reaction, ragdoll and (if lethal) death — the death then flows through
    // Agent.Die -> BattleAgentDiedPatch -> the normal death/casualty sync. Non-owners ignore it. No synthetic blow.
    private void Handle_NetworkApplyBattleDamage(MessagePayload<NetworkApplyBattleDamage> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            if (!registry.TryGetAgentInfo(payload.What.VictimAgentId, out var info)) return;
            if (info.CurrentAuthority != controllerIdProvider.ControllerId) return;

            var victim = info.Agent;
            var blow = payload.What.Blow;
            var collisionData = payload.What.CollisionData;
            var attackerId = payload.What.AttackerAgentId;

            if (Mission.Current == null || victim == null || !victim.IsActive() || victim.Health <= 0) return;

            // Re-map the attacker index to OUR local agent (indices are per-client); -1 if not resolvable here.
            if (attackerId != Guid.Empty && registry.TryGetAgentInfo(attackerId, out var attackerInfo) && attackerInfo.Agent != null)
                blow.OwnerId = attackerInfo.Agent.Index;
            else
                blow.OwnerId = -1;

            // Missile blows: the projectile is simulated only on the shooter (missiles aren't synced), so its
            // index is absent from THIS client's _missilesDictionary — Mission.OnAgentHit does
            // _missilesDictionary[index] for a missile blow and throws KeyNotFound. Clear the missile flag (the
            // publicizer exposes the private BlowWeaponRecord._isMissile) and the dangling projectile index so
            // OnAgentHit takes the no-missile path, while keeping the weapon class/flags for the hit reaction.
            // The already-resolved InflictedDamage still lands and the agent dies naturally. (A visible arrow on
            // this client would need real missile sync — a separate feature.)
            bool wasMissile = blow.IsMissile;
            if (wasMissile)
            {
                blow.WeaponRecord._isMissile = false;
                blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = -1;
            }

            // TEMP diagnostic: confirms the routed blow's damage/missile flag per hit. Remove once solid.
            Logger.Information("[BattleSync] Applying routed blow to {Agent}: dmg={Damage}, missile={Missile}, health={Health}",
                victim.Name, blow.InflictedDamage, wasMissile, victim.Health);
            victim.RegisterBlow(blow, in collisionData);

            // A hero's in-mission Agent.Health only propagates to the campaign Hero.HitPoints when the agent is
            // removed (Mission.OnAgentRemoved), so a wounded-but-SURVIVING hero's damage never reaches the server.
            // Mirror the owned hero's post-blow health onto Hero.HitPoints; HeroHitPointsRequestPatch then forwards
            // it to the server. A lethal blow is left to the death path (Agent.Die + the native removal set_HitPoints).
            if (victim.Health > 0 && victim.Character is CharacterObject character && character.IsHero && character.HeroObject is Hero hero)
                hero.HitPoints = Math.Max(1, (int)victim.Health);
        });
    }

    // A graceful leave is a RETREAT: the player withdraws, so their troops despawn on every client — the
    // OPPOSITE of a disconnect, where the host adopts them so they keep fighting. EXCEPTION: if the leaver
    // was the battle host, the server promotes a successor that adopts the battle instead (handled via
    // Handle_BattleHostMigrated), so don't despawn in that case.
    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        var controllerId = payload.What.ControllerId;
        if (payload.What.InstanceId != null && payload.What.InstanceId != instanceId) return;

        // EXCEPTION (see above): if the leaver was the battle HOST, a successor is promoted and adopts ALL of its
        // agents (own party AND the AI it ran) via Handle_BattleHostMigrated — exactly like a disconnect. Do NOT
        // also despawn here: the despawn and the adoption are independent queued game-thread actions, so on a host
        // retreat they RACE, leaving agents half faded-out and half adopted/AI-converted, which the movement poller
        // and the AI tick then dereference → a native crash. (A disconnect never despawns, which is why it migrates
        // cleanly.) The leave arrives before the host reassignment, so the registry still names the departing host.
        if (hostRegistry.TryGet(instanceId, out var assignment) && assignment.HostControllerId == controllerId)
        {
            Logger.Information("[BattleSync] Host {Controller} left — migration adopts its troops; skipping retreat despawn", controllerId);
            return;
        }

        // A NON-host retreat: withdraw only that player's OWN player-side troops; the host keeps running the AI.
        DespawnControllerTroops(controllerId);
    }

    // A disconnect (ungraceful drop) is NOT a retreat: the host adopts the dropped player's troops so they
    // keep fighting (or, on a host drop, a successor is promoted).
    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        HandlePeerGone(payload.What.ControllerId, payload.What.InstanceId, "disconnected");
    }

    // [All clients] Withdraw a retreating controller's OWN troops — only its player-SIDE agents. Enemy/AI
    // agents it owned (the host runs the AI) are left in place so the promoted successor can adopt them on
    // migration. Our own retreat tears the mission down (skip self); other clients drop its puppets. FadeOut
    // (not Die/MakeDead) so it is a withdrawal, not a casualty — the player keeps these troops on the map.
    private void DespawnControllerTroops(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (controllerId == controllerIdProvider.ControllerId) return;

        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            var troops = registry.GetAgents(controllerId);

            if (troops.Count == 0) return;
            if (Mission.Current == null) return;

            if (Mission.Current.PlayerTeam == null)
            {
                Logger.Error("PlayerTeam was not set");
                return;
            }

            var playerSide = Mission.Current.PlayerTeam.Side;
            int despawned = 0;
            foreach (var info in troops)
            {
                var agent = info.Agent;
                if (agent == null || agent.Team == null || agent.Team.Side != playerSide)
                    continue;

                if (agent.IsActive())
                    agent.FadeOut(false, true);
                registry.RemoveAgent(info.AgentId);
                _casualtyInfo.TryRemove(info.AgentId, out _);
                despawned++;
            }

            if (despawned > 0)
                Logger.Information("[BattleSync] Despawned {Count} retreating troop(s) of {Controller}", despawned, controllerId);
        });
    }

    // [Host] A player left/dropped from this battle. Their troops must not vanish: the current host adopts
    // them. Only the host acts (a non-host ignores it; on a host departure the server promotes a successor,
    // which adopts via Handle_BattleHostMigrated instead).
    private void HandlePeerGone(string controllerId, string goneInstanceId, string reason)
    {
        if (goneInstanceId != null && goneInstanceId != instanceId) return; // a different instance's churn
        if (!hostRegistry.IsLocalHost(instanceId)) return;

        AdoptAgentsFrom(controllerId, reason);
    }

    // [New host] The previous host departed and the server promoted us — adopt its orphaned agents (the
    // AI/enemy it was running, plus its own troops) so the battle continues under us. Published only to the
    // promoted client, so no host check here.
    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        if (payload.What.MapEventId != instanceId) return;
        AdoptAgentsFrom(payload.What.PreviousHostControllerId, "host migration");

        // If the battle was already live when we were promoted, release the NPC AI we just adopted — a still-
        // deploying new host has AI ticking off, which would otherwise hold them frozen even though they were
        // moving under the previous host. If the battle was not live yet, this is a no-op (the gate holds).
        _activator.OnPromotedToHost();
    }

    // Take over every agent currently owned by the departed controller: move authority to us (so the movement
    // poller broadcasts them and the death/casualty path owns them — their attribution was captured at spawn)
    // and convert each inert puppet into a host AI combatant. Other peers keep them as puppets that follow our
    // movement (their location-style despawn is suppressed during a coop battle).
    private void AdoptAgentsFrom(string controllerId, string reason)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (controllerId == controllerIdProvider.ControllerId) return;

        var registry = coopMissionComponent.AgentRegistry;
        var adopted = registry.GetAgents(controllerId);

        if (adopted.Count > 0)
        {
            foreach (var info in adopted)
                registry.TryTransferAuthority(controllerIdProvider.ControllerId, info.AgentId);

            GameThread.RunSafe(() =>
            {
                if (Mission.Current == null) return;

                // A migration can promote us while AI ticking is gated off (e.g. mid-deployment); turn it back on so
                // the adopted agents actually tick, exactly as ActivateNpcAi does.
                Mission.Current.AllowAiTicking = true;

                var formations = new HashSet<Formation>();
                int aiCount = 0;
                foreach (var info in adopted)
                {
                    var agent = info.Agent;
                    if (agent == null || !agent.IsActive()) continue;
                    ConvertPuppetToHostAi(agent);
                    if (agent.Controller == AgentControllerType.AI) aiCount++;
                    if (agent.Formation != null) formations.Add(agent.Formation);
                }

                // The converted agents are AI-controlled now, but in a coop battle no general commands their
                // formation — and Formation.SetControlledByAI only issues a movement order when the formation AI
                // has an active behavior (there is none here), so they'd stand idle. Give each adopted formation
                // an explicit Charge so the NPCs actually engage. (Freshly spawned troops move because their
                // OWNER's team AI drives them; these adopted puppets have no such driver and need the order.)
                foreach (var formation in formations)
                    formation.SetMovementOrder(MovementOrder.MovementOrderCharge);

                // TEMP diagnostic: how many adopted agents actually became AI-controlled, across how many
                // formations (each ordered to Charge above). The per-formation state is covered by the
                // BattleMigrationMirror E2E test.
                Logger.Information("[BattleSync] Adopt-AI: {AI}/{Total} now AI-controlled across {Forms} formation(s)",
                    aiCount, adopted.Count, formations.Count);
            });

            Logger.Information("[BattleSync] Adopted {Count} agent(s) from {Controller} ({Reason})",
                adopted.Count, controllerId, reason);
        }

        // We now own the departed controller's parties — pull our updated reserve from the server (the full
        // owned set at the current ledger pointers) so we can spawn their reinforcements from where the
        // departed owner left off. Runs even with no on-field agents adopted (reserve may still be unspawned).
        RequestReserves();
    }

    // [Owner, game thread] Ask the server for our current owned reserve (after adopting a departed owner's
    // parties). The reply re-sets our suppliers at the ledger pointers.
    private void RequestReserves()
    {
        if (instanceId == null) return;
        var id = instanceId;
        GameThread.RunSafe(() => relayNetwork.SendAll(new NetworkRequestBattleReserves(id, controllerIdProvider.ControllerId)));
    }

    // Turn an inert puppet (driven only by replicated movement) into a real AI combatant under the host's
    // command: place it in its team's formation for its troop class and hand it to the engine AI so it
    // maneuvers and fights like the host's own AI troops. The formation is set AI-controlled because in a
    // coop battle the host fights as a hero, not a general, so nothing would otherwise order it to engage.
    private static void ConvertPuppetToHostAi(Agent agent)
    {
        if (agent.Character != null && agent.Team != null)
        {
            var formation = agent.Team.GetFormation(agent.Character.GetFormationClass());
            if (formation != null)
            {
                agent.Formation = formation;
                formation.SetControlledByAI(true);
            }
        }

        agent.Controller = AgentControllerType.AI;
        agent.SetIsAIPaused(false);

        // Wake the AI the same way ActivateNpcAi does for the enemy side. Without this an adopted agent is
        // AI-controlled but NOT alarmed and holds stale enemy caches, so it ignores its formation's Charge order and
        // stands idle — the "allied NPCs don't move after host migration" bug. The ally side never goes through
        // ActivateNpcAi (which only releases the ENEMY side), so the adopt path must do the wake itself; adopted
        // agents are combat troops (the registry only holds riders, never mounts), so the CanWieldWeapon guard
        // ActivateNpcAi uses is unnecessary here.
        agent.SetAlarmState(Agent.AIStateFlag.Alarmed);
        agent.ResetEnemyCaches();
        agent.HumanAIComponent?.SyncBehaviorParamsIfNecessary();
    }

    private void SpawnPuppet(BattleAgentSpawnData data)
    {
        if (data.AgentId == Guid.Empty) return;

        // Spawn on the game thread, but do NOT block the network (receive) thread: while the mission is still
        // loading the game loop isn't draining the GameThread queue, so a blocking wait here deadlocks the
        // receive thread. If the mission's teams don't exist yet (a catch-up burst arriving mid-load), buffer
        // and retry on tick — an agent built before its team exists is team-less and later NREs the scoreboard.
        GameThread.RunSafe(() =>
        {
            if (!TrySpawnPuppetNow(data))
                lock (_pendingPuppetLock) _pendingPuppets.Add(data);
        });
    }

    // [Game thread] Spawn one puppet. Returns false (caller buffers) when the mission's teams aren't created
    // yet, so the agent is never built team-less.
    private bool TrySpawnPuppetNow(BattleAgentSpawnData data)
    {
        var registry = coopMissionComponent.AgentRegistry;

        if (Mission.Current == null) return true;                       // no mission — drop
        if (registry.TryGetAgentInfo(data.AgentId, out _)) return true; // already spawned — dedupe

        // While THIS client is still in its own Order-of-Battle phase, hold puppets OUT of the mission: a puppet
        // populates a team the native spawn gate (DefaultBattleMissionAgentSpawnLogic.CheckDeployment) inspects, but
        // with NO deployment plan (puppets aren't deployment-spawned) — which stalls the gate so this client's OWN
        // party/hero never spawn. Buffer until our deployment commits; DrainPendingPuppets then fields them.
        if (LocalDeploymentInProgress()) return false;                  // still deploying — buffer

        var team = ResolvePuppetTeam(data);
        if (team == null) return false;                                 // teams not created yet — buffer

        if (!TryResolveCharacter(data, out var character))
        {
            Logger.Warning("[BattleSync] Puppet skipped: unresolved character {Char} for agent {AgentId}", data.CharacterId, data.AgentId);
            return true;
        }

        // We own the agent when we are its owner — i.e. our own hero. That hero is adopted as the local main
        // agent and player-controlled; everything else is an inert puppet driven by its owner over the mesh.
        bool isOwnAgent = data.OwnerControllerId == controllerIdProvider.ControllerId;
        var equipment = character.IsHero ? character.HeroObject.BattleEquipment : character.Equipment;

        // Carry the troop's party so the agent has a real BattleCombatant — the battle observer/scoreboard
        // reads origin.BattleCombatant, and SimpleAgentOrigin leaves it null for non-hero troops.
        var party = ResolvePuppetParty(data.MapEventPartyId);
        IAgentOriginBase origin = party != null
            ? new CoopAgentOrigin(character, party, -1, null, new UniqueTroopDescriptor(data.TroopSeed))
            : new SimpleAgentOrigin(character, -1, null, default);

        var buildData = new AgentBuildData(character);
        buildData.BodyProperties(character.GetBodyPropertiesMax());
        buildData.InitialPosition(data.Position);
        buildData.Team(team);
        buildData.InitialDirection(Vec2.Forward);
        buildData.Equipment(equipment);
        buildData.TroopOrigin(origin);
        buildData.Controller(isOwnAgent ? AgentControllerType.Player : AgentControllerType.None);
        buildData.ClothingColor1(origin.FactionColor);
        buildData.ClothingColor2(origin.FactionColor2);

        // Suppress capture for the duration of this spawn: a puppet is another owner's troop replicated to us,
        // not ours, so BattleAgentSpawnedPatch must not re-capture and re-broadcast it.
        Agent agent;
        BattleSpawnGate.SuppressCapture = true;
        try
        {
            agent = Mission.Current.SpawnAgent(buildData);
        }
        finally
        {
            BattleSpawnGate.SuppressCapture = false;
        }
        agent.FadeIn();
        if (data.Health > 0) agent.Health = data.Health;

        // Adopt our own hero as the controllable main agent of this mission.
        if (isOwnAgent)
        {
            Mission.Current.MainAgent = agent;
        }
        else
        {
            // Keep the puppet un-paused so it follows its owner's movement even while THIS client is still in its
            // own deployment freeze (native deployment sets Mission.AllowAiTicking=false and AI-pauses agents). A
            // mid-battle joiner spawns these puppets while deploying into an ALREADY-LIVE battle; left paused, the
            // puppet never walks the small per-tick deltas its owner sends (AgentData.Apply only teleports on >1u
            // jumps), so the whole live battle looks frozen until the joiner clicks Start Battle. Mirrors the
            // adopt (ConvertPuppetToHostAi) and reinforcement (SpawnReinforcementTroop) paths, which un-pause too.
            agent.SetIsAIPaused(false);
        }

        registry.TryRegisterAgent(data.OwnerControllerId, data.AgentId, agent);
        // Key the casualty on the troop's CHARACTER through the object manager (never a raw StringId).
        objectManager.TryGetId(character, out var troopCharacterId);
        _casualtyInfo[data.AgentId] = (data.MapEventPartyId, data.TroopSeed, troopCharacterId);
        Logger.Information("[BattleSync] Spawned puppet {Char} (agent {AgentId}, ownAgent={Own})", data.CharacterId, data.AgentId, isOwnAgent);
        return true;
    }

    // True while THIS client is still in its own Order-of-Battle deployment — a deployment controller is attached
    // and we have not committed yet. Puppets are held until commit so they don't populate (plan-less) the teams the
    // native spawn gate inspects, which would stop this client's own troops from spawning. The deployment-controller
    // check keeps this false in the headless harness (no native controller there), so puppet tests are unaffected.
    private bool LocalDeploymentInProgress()
        => !_revealGate.IsCommitted
           && Mission.Current?.GetMissionBehavior<DeploymentMissionController>() != null;

    // [Game thread] Drain puppets buffered while the mission's teams did not yet exist (or while we were still
    // deploying), once they do / once our deployment has committed.
    private void DrainPendingPuppets()
    {
        if (Mission.Current == null || Mission.Current.DefenderTeam == null) return;
        if (LocalDeploymentInProgress()) return; // hold puppets until our own deployment commits

        BattleAgentSpawnData[] pending;
        lock (_pendingPuppetLock)
        {
            if (_pendingPuppets.Count == 0) return;
            pending = _pendingPuppets.ToArray();
            _pendingPuppets.Clear();
        }

        foreach (var data in pending)
        {
            // Per-puppet guard: one bad record must not abort the whole drain (and re-throw every tick). On
            // failure, drop it rather than re-buffering, so it can't spin a per-tick exception loop.
            try
            {
                if (!TrySpawnPuppetNow(data))
                    lock (_pendingPuppetLock) _pendingPuppets.Add(data);
            }
            catch (Exception e)
            {
                Logger.Error(e, "[BattleSync] Failed to spawn buffered puppet {AgentId}; dropping it", data.AgentId);
            }
        }
    }

    // The PartyBase for a battle party id (a MapEventParty object-manager id), used for a puppet's origin.
    private PartyBase ResolvePuppetParty(string mapEventPartyId)
    {
        if (mapEventPartyId != null && objectManager.TryGetObject<MapEventParty>(mapEventPartyId, out var mapEventParty))
            return mapEventParty?.Party;
        return null;
    }

    // The MapEventParty wrapping the given battle party — the casualty target on the server.
    private static MapEventParty ResolveMapEventParty(PartyBase party)
    {
        var side = party?.MapEventSide;
        if (side == null) return null;

        foreach (var mapEventParty in side.Parties)
        {
            if (mapEventParty?.Party == party)
                return mapEventParty;
        }
        return null;
    }

    // === Mid-battle reinforcements: field a newly-joined AI party through our own spawn path ===============

    // [Host] A party was added to the live battle. If it is a new AI party we field — not a player's own party,
    // and not one of the initial parties the troop supplier already spawns — field it now by spawning its troops
    // at the side's default reinforcement frame. Gated on the battle being activated so the INITIAL involved-
    // parties broadcast (pre-activation) is ignored: those parties spawn through the supplier, not here.
    private void Handle_ReinforcementPartiesAdded(MessagePayload<NetworkAddInvolvedParties> payload)
    {
        if (!hostRegistry.IsLocalHost(instanceId)) return;
        if (!_activator.IsActivated) return;

        var partyIds = payload.What.MapEventPartyIds;
        if (partyIds == null || partyIds.Length == 0) return;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            foreach (var partyId in partyIds)
            {
                if (_reinforcedParties.Contains(partyId)) continue;     // already fielded
                if (IsSupplierParty(partyId)) continue;                 // an initial party — the supplier spawns it
                if (!objectManager.TryGetObject<MapEventParty>(partyId, out var mapEventParty)) continue;

                var party = mapEventParty?.Party;
                if (party == null) continue;

                // The broadcast is server -> all clients (every battle), so only field this battle's parties.
                var mapEvent = party.MapEventSide?.MapEvent;
                if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId) || mapEventId != instanceId)
                    continue;

                // A player's own party is fielded by that player (Phase E), not us — we only field AI parties.
                if (party.LeaderHero?.IsPlayerHero() == true) continue;

                _reinforcedParties.Add(partyId);
                SpawnReinforcementParty(mapEventParty, party, partyId);
            }
        });
    }

    // Whether a party is one of the initial reserves the troop supplier already provides, so the native spawn
    // logic spawns it and we must not also spawn it here.
    private bool IsSupplierParty(string mapEventPartyId)
    {
        foreach (var supplier in CoopTroopSupplierRegistry.GetSuppliers(instanceId))
            foreach (var (partyId, _) in supplier.GetSuppliedByParty())
                if (partyId == mapEventPartyId) return true;
        return false;
    }

    // [Host, game thread] Field a newly-joined AI party: spawn each of its able troops AI-controlled at the
    // side's default reinforcement frame, then put the formations they land in on a charge. Capture is NOT
    // suppressed, so each spawn flows through Handle_AgentSpawnedInBattle (registered under us, broadcast to
    // peers as puppets, casualty attributed from the origin) — the same pipeline the initial troops use.
    private void SpawnReinforcementParty(MapEventParty mapEventParty, PartyBase party, string mapEventPartyId)
    {
        var mission = Mission.Current;
        var team = ResolveTeam(party.Side);
        if (team == null)
        {
            Logger.Warning("[BattleSync] No team for side {Side}; cannot field reinforcement party {Party}", party.Side, mapEventPartyId);
            return;
        }

        var formations = new HashSet<Formation>();
        int spawned = 0;
        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            var character = element.Character;
            if (character == null) continue;

            int able = element.Number - element.WoundedNumber;
            for (int i = 0; i < able; i++)
            {
                var agent = SpawnReinforcementTroop(mission, team, character, party);
                if (agent?.Formation != null) formations.Add(agent.Formation);
                spawned++;
            }
        }

        // A coop battle has no general commanding formations, so order each formation the reinforcements joined
        // to engage — SetControlledByAI alone leaves them idle without an active behavior.
        foreach (var formation in formations)
        {
            formation.SetControlledByAI(true);
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }

        Logger.Information("[BattleSync] Fielded reinforcement party {Party}: spawned {Count} troop(s)", mapEventPartyId, spawned);
    }

    // [Host, game thread] Spawn one reinforcement troop AI-controlled. With no InitialPosition set, the engine
    // positions it at the side's reinforcement spawn frame; we then drop it into its troop-class formation.
    private Agent SpawnReinforcementTroop(Mission mission, Team team, CharacterObject character, PartyBase party)
    {
        var origin = new CoopAgentOrigin(character, party, -1, null, new UniqueTroopDescriptor(MBRandom.RandomInt(int.MaxValue)));
        var equipment = character.IsHero ? character.HeroObject.BattleEquipment : character.Equipment;

        var buildData = new AgentBuildData(character);
        buildData.Team(team);
        buildData.TroopOrigin(origin);
        buildData.Equipment(equipment);
        buildData.BodyProperties(character.GetBodyPropertiesMax());
        buildData.Controller(AgentControllerType.AI);
        buildData.IsReinforcement(true);
        buildData.ClothingColor1(origin.FactionColor);
        buildData.ClothingColor2(origin.FactionColor2);

        var agent = mission.SpawnAgent(buildData);
        agent.FadeIn();

        if (agent.Character != null && agent.Team != null)
        {
            var formation = agent.Team.GetFormation(agent.Character.GetFormationClass());
            if (formation != null)
                agent.Formation = formation;
        }
        agent.SetIsAIPaused(false);

        return agent;
    }

    // The team a puppet joins. A puppet is ANOTHER owner's troop, so it must NOT land on our local PlayerTeam — the
    // Order-of-Battle deployment lets the local player arrange/command EVERY formation on PlayerTeam, so a puppet
    // there becomes deployable by us (the "non-host can deploy the host hero and NPC heroes" bug). Each client only
    // spawns its OWN party into PlayerTeam (the rest arrive here as puppets), so keeping puppets off PlayerTeam means
    // the local player only ever commands its own party. We put a puppet on a NON-player team for its side: the
    // side's main team if that isn't ours, otherwise the side's ally team. Returns null only while the side's main
    // team doesn't exist yet (mission still loading) so the caller buffers and retries.
    private Team ResolvePuppetTeam(BattleAgentSpawnData data)
    {
        var mainTeam = ResolveTeam(data.Side);
        if (mainTeam == null) return null;

        // Our OWN troop replicated back to us (e.g. our own-party deployment broadcast echoed over the mesh) belongs
        // on our own team — it is the one puppet we DO control.
        if (data.OwnerControllerId == controllerIdProvider.ControllerId)
            return mainTeam;

        var playerTeam = Mission.Current.PlayerTeam;
        if (mainTeam != playerTeam) return mainTeam;          // main team isn't ours (we're an ally) — safe to use

        // The side's main team IS our PlayerTeam, so route to the side's ally team instead so we can't command it.
        var allyTeam = data.Side == BattleSideEnum.Attacker
            ? Mission.Current.AttackerAllyTeam
            : Mission.Current.DefenderAllyTeam;
        if (allyTeam != null && allyTeam != playerTeam) return allyTeam;

        // No separate ally team on our side yet (only our own party present) — fall back to the main team.
        return mainTeam;
    }

    private static Team ResolveTeam(BattleSideEnum side)
    {
        return side switch
        {
            BattleSideEnum.Attacker => Mission.Current.AttackerTeam,
            BattleSideEnum.Defender => Mission.Current.DefenderTeam,
            _ => Mission.Current.PlayerEnemyTeam
        };
    }

    // Heroes and troops alike are carried by their CharacterObject id (hero CharacterObjects are registered —
    // CharacterObjectRegistry), so resolve uniformly. Hero-ness is recovered from the resolved character.IsHero.
    private bool TryResolveCharacter(BattleAgentSpawnData data, out CharacterObject character)
        => objectManager.TryGetObjectWithLogging(data.CharacterId, out character);
}
