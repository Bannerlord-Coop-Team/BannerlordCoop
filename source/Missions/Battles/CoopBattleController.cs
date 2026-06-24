using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
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
public class CoopBattleController : CoopMissionController, IBattleMissionBehavior
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleController>();

    private readonly INetwork relayNetwork;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IBattleHostRegistry hostRegistry;

    private string instanceId;
    private bool _instanceRequested;

    // Casualty attribution per agent (map-event party id + troop descriptor seed), captured at spawn and used
    // by the agent's owner to report its death to the server. Written from the main thread (host capture) and
    // the network thread (peer puppet spawn), read on death — hence concurrent.
    private readonly ConcurrentDictionary<Guid, (string mapEventPartyId, int troopSeed)> _casualtyInfo = new();

    // Throttled supply-progress reporting (game thread, via OnMissionTick): tells the server how far each of
    // our troop suppliers has spawned, so its ledger pointer advances and a new owner can resume from it on
    // disconnect/migration. Only sends when a count changed.
    private const float SupplyReportInterval = 1f;
    private float _supplyReportTimer;
    private readonly Dictionary<string, int> _lastSupplyReport = new();

    // Coop has no deployment UI to press "Start Battle", so the engine never turns on the local player's own
    // side spawner (only the AI side auto-spawns) — leaving the player with no troops/hero and an instant
    // loss. Force both sides' spawners on once; each client's supplier still only provides its owned troops.
    private bool _forcedSpawners;

    // Puppet spawns from the host's catch-up burst can arrive while THIS client's mission is still loading
    // (before MissionCombatantsLogic creates the teams). An agent built with a null team later NREs the
    // scoreboard (BattleObserverMissionLogic.SetObserver reads agent.Team.Side from its build cache), so buffer
    // such spawns and drain them on tick once the teams exist.
    private readonly object _pendingPuppetLock = new object();
    private readonly List<BattleAgentSpawnData> _pendingPuppets = new List<BattleAgentSpawnData>();

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

            var records = new List<BattleAgentSpawnData>();
            foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(controllerIdProvider.ControllerId))
            {
                var agent = info.Agent;
                if (agent == null || !agent.IsActive() || !(agent.Character is CharacterObject character)) continue;

                bool isHero = character.IsHero;
                object toResolve = isHero ? (object)character.HeroObject : character;
                if (toResolve == null || !objectManager.TryGetId(toResolve, out var characterId)) continue;

                _casualtyInfo.TryGetValue(info.AgentId, out var attribution);
                int side = agent.Team != null ? (int)agent.Team.Side : (int)BattleSideEnum.None;

                records.Add(new BattleAgentSpawnData(
                    info.AgentId, characterId, isHero, agent.Position, side, agent.Health,
                    controllerIdProvider.ControllerId, attribution.mapEventPartyId, attribution.troopSeed));
            }

            if (records.Count == 0) return;

            network.Send(controllerId, new NetworkSpawnBattleAgents(records.ToArray()));
            Logger.Information("[BattleSync] Replayed {Count} of our agent(s) to joining {Controller}", records.Count, controllerId);
        });
    }

    protected override void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo)
    {
        // Phase 0: log receipt to confirm the bidirectional handshake. Spawning the peer's troops is a
        // later phase (see CoopLocationsController.HandleJoinInfo for the spawn+register pattern).
        Logger.Information("[BattleSync] Received join info from {Controller} (instance {Instance})", joinInfo.ControllerId, instanceId);
    }

    protected override void OnLeaving()
    {
        BattleSpawnGate.EndBattle();

        if (instanceId != null)
        {
            CoopTroopSupplierRegistry.ClearBattle(instanceId);
            relayNetwork.SendAll(new NetworkMissionLeft(controllerIdProvider.ControllerId, instanceId));
            Logger.Information("[Relay] Announced MissionLeft for battle instance {Instance}", instanceId);
        }

        network.Stop();
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        EnsureSidesSpawning();
        DrainPendingPuppets();
        LogTeamDiagnosticOnce(dt);

        _supplyReportTimer += dt;
        if (_supplyReportTimer < SupplyReportInterval) return;
        _supplyReportTimer = 0f;
        ReportSupplyProgress();
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
            Logger.Information("[BattleDiag] Team side={Side} isPlayerTeam={IsPlayer} activeAgents={Count}",
                team.Side, team == mission.PlayerTeam, team.ActiveAgents.Count);

        var heroChar = Hero.MainHero?.CharacterObject;
        bool heroOnField = false;
        foreach (var agent in mission.Agents)
            if (agent.Character == heroChar) { heroOnField = true; break; }

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

    // Turn on both sides' spawners (the engine holds the player's own side for a deployment phase that coop
    // has no UI to complete). Idempotent; runs once the spawn logic exists.
    private void EnsureSidesSpawning()
    {
        if (_forcedSpawners) return;

        var spawnLogic = Mission.Current?.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();
        if (spawnLogic == null) return;

        spawnLogic.StartSpawner(BattleSideEnum.Attacker);
        spawnLogic.StartSpawner(BattleSideEnum.Defender);
        _forcedSpawners = true;
        Logger.Information("[BattleSync] Forced both side spawners on (coop has no deployment phase)");
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

        // Our own hero just spawned: the native deployment doesn't hand the player their agent in our coop
        // flow, so take control of it ourselves — set it as the mission's controllable main agent (the camera
        // follows the main agent). Done before the resolution/registration below so it runs even if those fail.
        if (character.IsHero && character.HeroObject == Hero.MainHero && Mission.Current != null && Mission.Current.MainAgent != agent)
        {
            agent.Controller = AgentControllerType.Player;
            Mission.Current.MainAgent = agent;
            Logger.Information("[BattleSync] Attached player to own hero agent ({Char})", character.StringId);
        }

        bool isHero = character.IsHero;
        object toResolve = isHero ? (object)character.HeroObject : character;
        if (toResolve == null || !objectManager.TryGetId(toResolve, out var characterId)) return;

        string owner = controllerIdProvider.ControllerId;
        var agentId = Guid.NewGuid();
        coopMissionComponent.AgentRegistry.TryRegisterAgent(owner, agentId, agent);

        // Casualty attribution: the battle-troop origin carries the map-event party and the exact troop
        // descriptor seed the server's OnTroopKilled path keys on. Carry them so we can report the casualty
        // on death (puppets, spawned with a SimpleAgentOrigin, get these from the spawn data).
        string mapEventPartyId = null;
        int troopSeed = 0;
        if (agent.Origin is PartyGroupAgentOrigin origin && origin.Party != null)
        {
            troopSeed = origin.TroopDesc.UniqueSeed;
            var mapEventParty = ResolveMapEventParty(origin.Party);
            if (mapEventParty != null && objectManager.TryGetId(mapEventParty, out var mepId))
                mapEventPartyId = mepId;
        }
        _casualtyInfo[agentId] = (mapEventPartyId, troopSeed);

        int side = agent.Team != null ? (int)agent.Team.Side : (int)BattleSideEnum.None;
        var data = new BattleAgentSpawnData(agentId, characterId, isHero, agent.Position, side, agent.Health, owner, mapEventPartyId, troopSeed);

        // SendAll over the mesh reaches every peer in this battle instance (not us).
        Logger.Information("[BattleSync] Captured own spawn {Char} (agent {AgentId}); broadcasting over mesh", characterId, agentId);
        network.SendAll(new NetworkSpawnBattleAgents(new[] { data }));
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
        if (!registry.TryGetAgentInfo(payload.What.Agent, out var info)) return;
        if (info.CurrentAuthority != controllerIdProvider.ControllerId) return;

        network.SendAll(new NetworkBattleAgentDied(info.AgentId, payload.What.Wounded));

        // Owner-authoritative casualty: tell the server to account this troop's death/wound against its
        // map-event party roster. The host's own mission accounting is suppressed during a coop battle
        // (MapEventPartyPatches), so this is the single source. On a client, SendAll targets the server.
        if (_casualtyInfo.TryGetValue(info.AgentId, out var attribution) && attribution.mapEventPartyId != null)
            relayNetwork.SendAll(new NetworkRequestBattleCasualty(attribution.mapEventPartyId, attribution.troopSeed, payload.What.Wounded));

        _casualtyInfo.TryRemove(info.AgentId, out _);
        registry.RemoveAgent(info.AgentId);
    }

    // [Peer] The owner reported one of its agents died — kill our puppet of it and deregister.
    private void Handle_NetworkBattleAgentDied(MessagePayload<NetworkBattleAgentDied> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(payload.What.AgentId, out var info)) return;

        Agent agent = info.Agent;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            if (agent != null && agent.Health > 0)
            {
                agent.MakeDead(false, ActionIndexCache.act_none);
                agent.FadeOut(false, true);
            }
        });

        registry.RemoveAgent(payload.What.AgentId);
        _casualtyInfo.TryRemove(payload.What.AgentId, out _);
    }

    // [Attacker's node] A local troop hit a puppet (damage suppressed locally by BattleBlowInterceptPatch).
    // Route the damage to the puppet over the mesh; only its owner will apply it.
    private void Handle_BattlePuppetHit(MessagePayload<BattlePuppetHit> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(payload.What.Victim, out var info)) return;

        network.SendAll(new NetworkApplyBattleDamage(info.AgentId, payload.What.Damage));
    }

    // [Owner] Another client's troop hit one of OUR agents. Apply the damage authoritatively; if it dies, the
    // Agent.Die chokepoint feeds the normal death sync (NetworkBattleAgentDied + server casualty). Non-owners
    // ignore the message — the agent's life/death is decided only here.
    private void Handle_NetworkApplyBattleDamage(MessagePayload<NetworkApplyBattleDamage> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(payload.What.VictimAgentId, out var info)) return;
        if (info.CurrentAuthority != controllerIdProvider.ControllerId) return;

        var agent = info.Agent;
        var damage = payload.What.Damage;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null || agent == null || !agent.IsActive() || agent.Health <= 0) return;

            agent.Health -= damage;
            if (agent.Health < 1f)
                agent.Die(CreateFatalBlow(agent), Agent.KillInfo.Invalid);
        });
    }

    // A minimal lethal blow so the agent dies through the normal Agent.Die path (which triggers our death
    // capture/broadcast). Self-attributed; routed damage doesn't carry the attacker for v1.
    private static Blow CreateFatalBlow(Agent agent)
    {
        return new Blow(agent.Index)
        {
            DamageType = DamageTypes.Pierce,
            BaseMagnitude = 100000f,
            InflictedDamage = 100000,
            DamagedPercentage = 1f,
            DamageCalculated = true,
            GlobalPosition = agent.Position,
            VictimBodyPart = BoneBodyPartType.Head,
        };
    }

    // A graceful leave is a RETREAT: the player withdraws, so their troops despawn on every client — the
    // OPPOSITE of a disconnect, where the host adopts them so they keep fighting. EXCEPTION: if the leaver
    // was the battle host, the server promotes a successor that adopts the battle instead (handled via
    // Handle_BattleHostMigrated), so don't despawn in that case.
    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        var controllerId = payload.What.ControllerId;
        if (payload.What.InstanceId != null && payload.What.InstanceId != instanceId) return;

        // Despawn the departing player's OWN troops (their player-side party). If they were the HOST, the AI
        // they ran is NOT despawned (DespawnControllerTroops only touches player-side agents) — it migrates to
        // the promoted successor, which adopts it via Handle_BattleHostMigrated. The server sends this leave
        // before the host reassignment, so the player party is gone from the registry by the time the
        // successor adopts, leaving it just the AI.
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
        var troops = registry.GetAgents(controllerId);
        if (troops.Count == 0) return;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            // In coop the human players share one side; the AI is the enemy side. Only the departing player's
            // own (player-side) party withdraws — the AI it may have hosted stays for the new host to adopt.
            var playerSide = Mission.Current.PlayerTeam?.Side ?? BattleSideEnum.Defender;
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
                foreach (var info in adopted)
                {
                    var agent = info.Agent;
                    if (agent == null || !agent.IsActive()) continue;
                    ConvertPuppetToHostAi(agent);
                }
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

        var team = ResolveTeam(data.Side);
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
            Mission.Current.MainAgent = agent;

        registry.TryRegisterAgent(data.OwnerControllerId, data.AgentId, agent);
        _casualtyInfo[data.AgentId] = (data.MapEventPartyId, data.TroopSeed);
        Logger.Information("[BattleSync] Spawned puppet {Char} (agent {AgentId}, ownAgent={Own})", data.CharacterId, data.AgentId, isOwnAgent);
        return true;
    }

    // [Game thread] Drain puppets buffered while the mission's teams did not yet exist, once they do.
    private void DrainPendingPuppets()
    {
        if (Mission.Current == null || Mission.Current.DefenderTeam == null) return;

        BattleAgentSpawnData[] pending;
        lock (_pendingPuppetLock)
        {
            if (_pendingPuppets.Count == 0) return;
            pending = _pendingPuppets.ToArray();
            _pendingPuppets.Clear();
        }

        foreach (var data in pending)
            if (!TrySpawnPuppetNow(data))
                lock (_pendingPuppetLock) _pendingPuppets.Add(data);
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

    private static Team ResolveTeam(int side)
    {
        return (BattleSideEnum)side switch
        {
            BattleSideEnum.Attacker => Mission.Current.AttackerTeam,
            BattleSideEnum.Defender => Mission.Current.DefenderTeam,
            _ => Mission.Current.PlayerEnemyTeam
        };
    }

    private bool TryResolveCharacter(BattleAgentSpawnData data, out CharacterObject character)
    {
        character = null;
        if (data.IsHero)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.CharacterId, out var hero)) return false;
            character = hero.CharacterObject;
            return character != null;
        }

        return objectManager.TryGetObjectWithLogging(data.CharacterId, out character);
    }
}
