using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Missions.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Locations.Hosting;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Missions.Data;
using Missions.Agents.Packets;
using Missions.Locations;
using Missions.Services.Network;
using SandBox.Missions.MissionLogics;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Taverns;

public class CoopLocationsController : CoopMissionController, ILocationMissionBehavior
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopLocationsController>();
    private readonly INetwork relayNetwork;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ILocationSession session;
    private readonly ILocationHostRegistry hostRegistry;
    private readonly ILocationOwnedAgentReplicator npcReplicator;
    private readonly ILocationPuppetSpawner npcPuppetSpawner;
    private readonly ILocationPopulationDirector populationDirector;
    private readonly ILocationAuthorityMigrator authorityMigrator;
    private readonly ILocationPartyAgentMap partyAgentMap;
    private readonly IMissionWeaponDataMapper missionWeaponDataMapper;
    //private readonly BoardGameManager boardGameManager;

    private string instanceId;
    public CoopLocationsController(
        IBattleNetwork network,
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider,
        ILocationHostRegistry hostRegistry,
        ILocationPuppetRosterBinder rosterBinder,
        ILocationNpcHoldRegistry npcHoldRegistry,
        IBattleAgentBudget agentBudget,
        ILocationAgentSpawnBatchCodec spawnBatchCodec,
        ILocationControllerWithdrawalState withdrawalState,
        IMissionContext missionContext,
        IMissionWeaponDataMapper missionWeaponDataMapper,
        //BoardGameManager boardGameManager,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent)
        : base(
            network,
            messageBroker,
            objectManager,
            coopMissionComponent,
            Missions.Agents.Handlers.MovementCadenceProfile.Location)
    {
        this.relayNetwork = relayNetwork;
        this.controllerIdProvider = controllerIdProvider;
        this.hostRegistry = hostRegistry;
        this.missionWeaponDataMapper = missionWeaponDataMapper;
        //this.boardGameManager = boardGameManager;

        // Composition-root style (mirrors CoopBattleController): the per-mission session and NPC
        // binding map are SHARED state, so the components are constructed here around single
        // instances instead of DI-resolving them (transient injection would give each its own).
        session = new LocationSession(controllerIdProvider, hostRegistry);
        coopMissionComponent.WeaponDropHandler.ConfigureLocalHostProvider(
            () => session.IsLocalHost);
        var bindingMap = new LocationAgentBindingMap();
        partyAgentMap = new LocationPartyAgentMap();

        npcReplicator = new LocationOwnedAgentReplicator(
            network, messageBroker, objectManager, coopMissionComponent, session, bindingMap, rosterBinder, spawnBatchCodec);
        authorityMigrator = new LocationAuthorityMigrator(
            messageBroker, coopMissionComponent, session, bindingMap, partyAgentMap, missionContext, npcHoldRegistry);
        npcPuppetSpawner = new LocationPuppetSpawner(
            messageBroker, objectManager, coopMissionComponent, session, bindingMap, partyAgentMap,
            rosterBinder, agentBudget, spawnBatchCodec, authorityMigrator, withdrawalState);
        populationDirector = new LocationPopulationDirector(messageBroker, session, bindingMap, npcPuppetSpawner);

        messageBroker.Subscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);

        npcReplicator.Dispose();
        npcPuppetSpawner.Dispose();
        populationDirector.Dispose();
        authorityMigrator.Dispose();

        base.Dispose();
    }

    public override void OnMissionTick(float dt)
    {
        // Host: flush captured spawns as batches BEFORE polling movement, so a puppet exists on the
        // receiver before its first movement packet; point-use transitions go out after the spawns
        // on the same reliable stream; then drain any buffered puppets.
        npcReplicator.FlushPendingSpawns();
        npcReplicator.PollPointUsage();
        base.OnMissionTick(dt);

        // Mission.OnAgentCreated fires before the engine assigns Agent.Origin. Re-scan after the native tick so
        // companions spawned dynamically (for example by a passage transition) are registered once their
        // PartyAgentOrigin is available.
        if (_localAgentRegistered)
            TryRegisterLocalPartyAgents();

        npcPuppetSpawner.DrainPendingPuppets();
    }

    // SR-026/SR-041 (V8): the engine's removal virtuals are the host-side despawn capture — no
    // Harmony needed. OnAgentRemoved fires for deaths/knock-outs, OnAgentDeleted when a faded-out
    // agent is deleted (passage exits, churn). NotifyAgentRemoved ignores anything that is not a
    // replicated NPC we own, and the teardown guard keeps a local mission end from broadcasting the
    // whole crowd as despawns (peers handle our departure via mission membership instead).
    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        if (IsMissionEnding()) return;

        var reason = agentState == AgentState.Killed || agentState == AgentState.Unconscious
            ? LocationDespawnReason.Died
            : LocationDespawnReason.Removed;
        if (TryDespawnOwnedCompanion(affectedAgent, reason)) return;

        npcReplicator.NotifyAgentRemoved(affectedAgent, reason);
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        base.OnAgentDeleted(affectedAgent);
        if (IsMissionEnding()) return;
        if (TryDespawnOwnedCompanion(affectedAgent, LocationDespawnReason.Removed)) return;

        npcReplicator.NotifyAgentRemoved(affectedAgent, LocationDespawnReason.Removed);
    }

    private bool IsMissionEnding()
    {
        var mission = Mission.Current;
        return mission == null
            || mission.CurrentState == Mission.State.EndingNextFrame
            || mission.CurrentState == Mission.State.Over;
    }

    // Read on the network thread (HandleJoinInfo gate) and written on the main thread
    // (TryRegisterLocalPartyAgents), so volatile to ensure the gate sees the flip promptly.
    private volatile bool _localAgentRegistered;
    private bool _instanceRequested;
    private bool _spawningOwnedCompanions;
    // AgentDeathHandler may remove a killed agent from the shared registry before Mission.OnAgentRemoved.
    // Retain companion ids here so passage exits and deaths can still be announced exactly once.
    private readonly Dictionary<Agent, Guid> _ownedCompanionIds = new Dictionary<Agent, Guid>();

    // Join info can arrive before the local interior mission has finished setting up (teams/player
    // agent) — notably on a rejoin, where the kept-alive socket reconnects and delivers the peer's
    // join info almost immediately. Spawning a remote agent into a not-yet-initialized mission corrupts
    // team setup, so buffer early join info and drain it once we are ready (see TryRegisterLocalPartyAgents).
    private readonly ConcurrentQueue<(NetPeer peer, NetworkMissionJoinInfo info)> _pendingJoinInfos
        = new ConcurrentQueue<(NetPeer, NetworkMissionJoinInfo)>();

    public override void OnRenderingStarted()
    {
        // The elected NPC host owns the ambient population, but each player always owns their party companions.
        // Recover any companion roster entry that vanilla's initial accompanying-character pass did not materialize;
        // agents it already spawned are deduped by their shared PartyAgentOrigin.
        SpawnOwnedCompanionRosterAgents();
        TryRegisterLocalPartyAgents();
    }

    public override void OnAgentCreated(Agent agent)
    {
        base.OnAgentCreated(agent);

        // The explicit owner-side roster pass finishes the agent's native location behaviors before registering
        // and announcing it. Avoid re-entering registration from Mission.SpawnAgent midway through that pass.
        if (_spawningOwnedCompanions) return;

        // Some spawn paths already expose the PartyAgentOrigin here; register those immediately. The post-native
        // tick scan is the backstop for the usual engine order where Origin is assigned after this callback.
        if (IsOwnPartyAgent(agent))
        {
            TryRegisterLocalPartyAgents(agent);
        }
    }

    private void SpawnOwnedCompanionRosterAgents()
    {
        Mission mission = Mission.Current;
        Location location = CampaignMission.Current?.Location;
        MissionAgentHandler agentHandler = mission?.GetMissionBehavior<MissionAgentHandler>();
        if (mission == null || location == null || agentHandler == null) return;

        _spawningOwnedCompanions = true;
        bool previousSuppressCapture = LocationNpcGate.SuppressCapture;
        LocationNpcGate.SuppressCapture = true;
        int ownedRosterEntries = 0;
        int recoverySpawns = 0;
        try
        {
            foreach (LocationCharacter locationCharacter in location.GetCharacterList())
            {
                if (!IsOwnPartyCompanion(locationCharacter)) continue;
                ownedRosterEntries++;

                bool alreadySpawned = false;
                foreach (Agent agent in mission.Agents)
                {
                    if (ReferenceEquals(agent.Origin, locationCharacter.AgentOrigin))
                    {
                        alreadySpawned = true;
                        break;
                    }
                }

                if (alreadySpawned)
                {
                    Logger.Information("[LocationCompanion] Roster entry {Character} already has a native mission agent",
                        locationCharacter.Character?.StringId ?? "<null>");
                    continue;
                }

                Logger.Warning("[LocationCompanion] Vanilla did not materialize roster entry {Character}; attempting owner-side recovery spawn in {Location}",
                    locationCharacter.Character?.StringId ?? "<null>", location.StringId);
                Agent companion = agentHandler.SpawnDefaultLocationCharacter(locationCharacter);
                if (companion == null)
                {
                    Logger.Warning("[LocationCompanion] Recovery spawn failed for local companion {Character} in {Location}",
                        locationCharacter.Character?.StringId ?? "<null>", location.StringId);
                }
                else
                {
                    recoverySpawns++;
                    Logger.Information("[LocationCompanion] Recovery spawn created {Character} at {Position}",
                        locationCharacter.Character?.StringId ?? "<null>", companion.Position);
                }
            }
        }
        finally
        {
            LocationNpcGate.SuppressCapture = previousSuppressCapture;
            _spawningOwnedCompanions = false;
            Logger.Information("[LocationCompanion] Owner roster reconciliation complete for {Location}: ownedRosterEntries={RosterCount}, recoverySpawns={RecoveryCount}",
                location.StringId, ownedRosterEntries, recoverySpawns);
        }
    }

    private static bool IsOwnPartyCompanion(LocationCharacter locationCharacter)
    {
        if (!(locationCharacter?.AgentOrigin is PartyAgentOrigin origin)) return false;
        return origin.Party == PartyBase.MainParty && locationCharacter.Character != CharacterObject.PlayerCharacter;
    }

    private void TryRegisterLocalPartyAgents(Agent newlyCreatedAgent = null)
    {
        Agent mainAgent = Agent.Main;
        Mission mission = Mission.Current;
        if (mainAgent == null || mission == null) return;

        string controllerId = controllerIdProvider.ControllerId;
        var agentRegistry = coopMissionComponent.AgentRegistry;
        bool registryChanged = TryRegisterOwnedAgent(controllerId, mainAgent, "player");

        // Vanilla's LocationEncounter materializes the local player's accompanying companions only on this
        // client. Their native controllers stay AI; registering them here makes the shared movement/action
        // paths treat this node as authoritative while peers keep controller-less puppets.
        foreach (Agent agent in mission.Agents)
        {
            if (ReferenceEquals(agent, mainAgent) || !IsOwnPartyAgent(agent) ||
                !agent.IsActive() || agent.Health <= 0) continue;
            registryChanged |= TryRegisterOwnedAgent(controllerId, agent, "companion");
        }

        // Mission.OnAgentCreated can run before the new agent appears in Mission.Agents on some engine paths.
        // Include the callback argument explicitly; registry deduplication makes this harmless when it is listed.
        if (newlyCreatedAgent != null &&
            !ReferenceEquals(newlyCreatedAgent, mainAgent) &&
            IsOwnPartyAgent(newlyCreatedAgent) &&
            newlyCreatedAgent.IsActive() && newlyCreatedAgent.Health > 0)
        {
            registryChanged |= TryRegisterOwnedAgent(controllerId, newlyCreatedAgent, "companion");
        }

        if (!agentRegistry.TryGetAgentInfo(mainAgent, out _)) return;

        bool becameReady = !_localAgentRegistered;
        _localAgentRegistered = true;

        if (registryChanged)
        {
            // Announce the complete locally-owned party. The receiver dedupes existing ids, so this also handles
            // companions that spawn after the initial player announcement.
            network.SendAll(BuildJoinInfo());
        }

        if (becameReady)
        {
            // NPC puppet spawns wait until the local player agent and mission teams are ready.
            npcPuppetSpawner.NotifyMissionReady();

            // The mission is now set up (player agent + teams exist). Spawn any join info that arrived early.
            DrainPendingJoinInfos();

            if (instanceId != null)
                messageBroker.Publish(this, new LocationMissionReady(instanceId));
        }
    }

    private bool TryRegisterOwnedAgent(string controllerId, Agent agent, string agentType)
    {
        var agentRegistry = coopMissionComponent.AgentRegistry;
        if (agentRegistry.TryGetAgentInfo(agent, out var existingInfo))
        {
            partyAgentMap.Record(existingInfo.AgentId);
            RememberOwnedCompanion(agent, existingInfo.AgentId);
            return false;
        }

        Guid agentId = Guid.NewGuid();
        if (!agentRegistry.TryRegisterAgent(controllerId, agentId, agent)) return false;

        partyAgentMap.Record(agentId);
        RememberOwnedCompanion(agent, agentId);
        Logger.Information("[LocationSync] Registered local {AgentType} {AgentId} for {ControllerId}",
            agentType, agentId, controllerId);
        return true;
    }

    private void RememberOwnedCompanion(Agent agent, Guid agentId)
    {
        if (!ReferenceEquals(agent, Agent.Main))
            _ownedCompanionIds[agent] = agentId;
    }

    private bool TryDespawnOwnedCompanion(Agent agent, LocationDespawnReason reason)
    {
        if (agent == null || !_ownedCompanionIds.TryGetValue(agent, out Guid agentId)) return false;

        _ownedCompanionIds.Remove(agent);
        coopMissionComponent.AgentRegistry.RemoveAgent(agentId);
        network.SendAll(new NetworkDespawnLocationAgents(
            new[] { agentId },
            new[] { (byte)reason },
            new[] { string.Empty }));
        Logger.Information("[LocationSync] Despawned local companion {AgentId} ({Reason})", agentId, reason);
        return true;
    }

    internal static bool IsOwnPartyAgent(Agent agent)
    {
        if (agent == null) return false;
        if (ReferenceEquals(agent, Agent.Main)) return true;

        return agent.Origin is PartyAgentOrigin origin && origin.Party == PartyBase.MainParty;
    }

    private void DrainPendingJoinInfos()
    {
        if (_localAgentRegistered == false) return;

        while (_pendingJoinInfos.TryDequeue(out var pending))
        {
            ProcessJoinInfo(pending.peer, pending.info);
        }
    }

    // The interior mission was opened locally. This controller is attached to the mission by the
    // OpenIndoorMission postfix BEFORE the event is published, so it is the live, mission-scoped owner
    // of the P2P connection. The instance id is derived locally from (settlement, location): the
    // server creates the instance on the first NAT punch, so no separate assignment round-trip is
    // needed and both co-located clients independently compute the same id.
    private void Handle_PlayerEnteredLocation(MessagePayload<PlayerEnteredLocation> payload)
    {
        // OpenIndoorMission fires several times per entry; connect once per mission.
        if (_instanceRequested) return;

        var data = payload.What;

        if (LocationInstanceId.TryDerive(objectManager, data.Settlement, data.Location, out var derivedInstanceId) == false)
        {
            Logger.Warning("[LocationSync] Could not derive instance id for settlement '{Settlement}' location '{Location}' — skipping instance request",
                data.Settlement?.StringId ?? "<null>", data.Location?.StringId ?? "<null>");
            return;
        }

        _instanceRequested = true;
        Logger.Information("[LocationSync] Requesting P2P instance {InstanceId}", derivedInstanceId);

        network.Start();

        instanceId = derivedInstanceId;
        session.TryBegin(instanceId);

        // A fresh mission has no host yet: drop any assignment left from a PREVIOUS visit to this
        // settlement (the server clears only its own entry when an instance empties), or a stale
        // host would read as "previous host" and fake a migration when the new election lands.
        hostRegistry.Remove(instanceId);

        // Engage the NPC gate: native population spawning is suppressed on every client until the
        // server's host assignment confirms who runs it (SR-013).
        LocationNpcGate.BeginMission(instanceId);

        network.ConnectToInstance(instanceId);
        coopMissionComponent.AgentRegistry.Clear();

        relayNetwork.SendAll(new NetworkMissionEntered(controllerIdProvider.ControllerId, instanceId));
        Logger.Information("[Relay] Announced MissionEntered for instance {Instance}", instanceId);
    }

    private NetworkMissionJoinInfo BuildJoinInfo()
    {
        Agent mainAgent = Agent.Main;
        bool isPlayerAlive = mainAgent != null && mainAgent.Health > 0;
        var agents = new List<CoopAgentSpawnData>();

        foreach (var agentInfo in coopMissionComponent.AgentRegistry.GetAgents(controllerIdProvider.ControllerId))
        {
            Agent agent = agentInfo.Agent;
            // The local controller may also be the elected ambient-NPC host. Those agents have their own
            // NetworkSpawnLocationAgents catch-up path with roster bindings; never leak them into party join info.
            if (agent == null || !IsOwnPartyAgent(agent) || !(agent.Character is CharacterObject character)) continue;
            if (!ReferenceEquals(agent, mainAgent) && (!agent.IsActive() || agent.Health <= 0)) continue;
            if (!objectManager.TryGetIdWithLogging(character, out var characterObjectId)) continue;

            agents.Add(new CoopAgentSpawnData(
                agentInfo.AgentId,
                characterObjectId,
                agent.Position,
                agent.Health,
                isPlayer: ReferenceEquals(agent, mainAgent),
                hasMount: agent.HasMount,
                missionEquipmentData: PackMissionEquipmentData(agent.Equipment),
                currentEquipment: new AgentEquipmentData(agent)));
        }

        return new NetworkMissionJoinInfo(
            controllerIdProvider.ControllerId,
            isPlayerAlive,
            agents.ToArray()
        );
    }

    protected override void SendJoinInfo(string controllerId)
    {
        Logger.Debug("Sending join request");

        if (_localAgentRegistered == false || Agent.Main == null)
        {
            Logger.Information("[LocationSync] Skipping join info to {Controller} — local party not registered yet (will re-announce on render)", controllerId);
            return;
        }

        NetworkMissionJoinInfo request = null;
        GameThread.RunSafe(() => request = BuildJoinInfo(), blocking: true);
        if (request == null) return;

        network.Send(controllerId, request);
        Logger.Information("Sent Join Request for {PlayerID} to {Controller}", request.ControllerId, controllerId);

        // Catch the joiner up on the settlement NPCs we own (SR-025) — only the host owns any, so
        // this is a no-op everywhere else.
        npcReplicator.ReplicateCurrentAgentsTo(controllerId);
        if (session.IsLocalHost)
            coopMissionComponent.WeaponDropHandler.CatchUpJoiner(controllerId);
    }

    protected override void OnLeaving()
    {
        LocationNpcGate.EndMission();
        relayNetwork.SendAll(new NetworkMissionLeft(controllerIdProvider.ControllerId, instanceId));
        messageBroker.Publish(this, new PlayerLeftLocation());
        network.Stop();
    }

    protected override void HandleJoinInfo(NetPeer netPeer, NetworkMissionJoinInfo joinInfo)
    {
        // Spawning needs the interior mission fully set up (player agent + teams). On a rejoin the join
        // info beats the mission setup, so buffer it and drain once we are ready (TryRegisterLocalPartyAgents).
        // Re-check readiness after enqueuing to close the race with the main thread flipping it.
        if (_localAgentRegistered == false)
        {
            _pendingJoinInfos.Enqueue((netPeer, joinInfo));
            Logger.Information("[LocationSync] Mission not ready — buffered join info for {ControllerId}", joinInfo.ControllerId);
            DrainPendingJoinInfos();
            return;
        }

        ProcessJoinInfo(netPeer, joinInfo);
    }

    private void ProcessJoinInfo(NetPeer netPeer, NetworkMissionJoinInfo joinInfo)
    {
        foreach (var agentData in joinInfo.AiAgentData)
        {
            ProcessAgent(joinInfo.ControllerId, agentData);
        }
    }

    private void ProcessAgent(string controllerId, CoopAgentSpawnData agentData)
    {
        if (agentData.AgentId == Guid.Empty)
        {
            Logger.Warning("[LocationSync] Join info from {ControllerId} has no agent id — skipping", controllerId);
            return;
        }

        var agentRegistry = coopMissionComponent.AgentRegistry;

        // Record party identity before dedupe. An ambient spawn batch from the elected host can race this
        // join record; even if that batch won and attached an NPC binding, migration must still despawn this
        // player/companion instead of adopting it as settlement population.
        partyAgentMap.Record(agentData.AgentId);

        // Dedupe across all peers: NAT punch can yield more than one connection to the same remote
        // client, delivering its join info multiple times. Only spawn one agent per id.
        if (agentRegistry.TryGetAgentInfo(agentData.AgentId, out _))
        {
            // On a clean rejoin this should NOT fire — if it does, the leaver's agent was left in the
            // registry on leave/disconnect (stale collection), which blocks the re-spawn.
            Logger.Information("[LocationSync] Agent {AgentID} already registered — skipping spawn (expected only for duplicate NAT connections, NOT on rejoin)", agentData.AgentId);
            return;
        }

        if (!objectManager.TryGetObjectWithLogging(agentData.CharacterObjectId, out CharacterObject characterObject))
            return;

        Logger.Information("Spawning {AgentType} called {AgentName}({AgentID}) from {Peer}",
            agentData.IsPlayer == true ? "Player" : "Agent",
            characterObject?.Name?.ToString() ?? "<unresolved>", agentData.AgentId, controllerId);

        Agent newAgent = SpawnAgent(
            agentData.Position,
            characterObject,
            agentData.HasMount,
            agentData.Health,
            agentData.MissionEquipmentData,
            agentData.HasCurrentEquipment ? agentData.CurrentEquipment : (AgentEquipmentData?)null);

        if (newAgent == null)
        {
            Logger.Error("[LocationSync] Failed to spawn remote agent {AgentID} — removing agent.", agentData.AgentId);
            agentRegistry.RemoveAgent(agentData.AgentId);
            return;
        }

        agentRegistry.TryRegisterAgent(controllerId, agentData.AgentId, newAgent);
        Logger.Information("[LocationSync] Spawned + registered remote agent {AgentID} at {Pos} (mission '{Scene}')",
            agentData.AgentId, newAgent.Position, Mission.Current?.SceneName);
    }

    public Agent SpawnAgent(
        Vec3 startingPos,
        CharacterObject character,
        bool hasMount = false,
        float health = -1f,
        MissionEquipmentData missionEquipmentData = null,
        AgentEquipmentData? currentEquipment = null)
    {
        // A remote player's hero CharacterObject often does not resolve to a fully-initialized
        // object on this client (live campaign: each player has a distinct, not-yet-synced hero),
        // so GetBodyPropertiesMax / FirstCivilianEquipment NRE internally. Try the supplied
        // character, then fall back to the local player character so an agent still spawns.
        // (Proper fix: sync the remote player's hero identity — see doc/LocationSync.md §7.)
        if (LooksUsable(character) == false)
        {
            Logger.Warning("[LocationSync] Remote CharacterObject '{Name}' looks unresolved (null culture/etc). " +
                "Falling back to the local player character so an agent still spawns. REPORT THIS.",
                character?.StringId ?? "<null>");
            return null;
        }

        // HandleJoinInfo runs on the network thread. AgentBuildData's ctor (and SpawnAgent) touch
        // TaleWorlds engine statics (Team.Invalid -> Team.Initialize -> Formation.Reset) that must run
        // on the main thread, so build AND spawn entirely inside the game-loop closure — not just the
        // final SpawnAgent call. Doing the ctor off-thread NREs intermittently (notably on rejoin).
        Agent agent = null;
        GameThread.RunSafe(() =>
        {
            try
            {
                // The player may have left between receiving the join info and this running.
                if (Mission.Current == null) return;

                // The owner sends the live mount state because companions can spawn mounted in village centers.
                bool isVillage = Settlement.CurrentSettlement?.IsVillage == true;

                AgentBuildData agentBuildData = new AgentBuildData(character);
                agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
                agentBuildData.InitialPosition(startingPos);
                agentBuildData.Team(Mission.Current.PlayerAllyTeam);
                agentBuildData.InitialDirection(Vec2.Forward);
                agentBuildData.NoHorses(ShouldDisableHorses(hasMount));
                agentBuildData.Equipment(isVillage ? character.FirstBattleEquipment : character.FirstCivilianEquipment);
                MissionEquipment missionEquipment = ResolveMissionEquipment(missionEquipmentData);
                if (missionEquipment != null)
                    agentBuildData.MissionEquipment(missionEquipment);
                agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default));
                agentBuildData.Controller(AgentControllerType.None);
                agentBuildData.ClothingColor1(character.HeroObject.MapFaction.Color);
                agentBuildData.ClothingColor2(character.HeroObject.MapFaction.Color2);

                // Remote party puppets are not host-owned NPCs and must not be captured for replication.
                LocationNpcGate.SuppressCapture = true;
                try
                {
                    agent = Mission.Current.SpawnAgent(agentBuildData);
                }
                finally
                {
                    LocationNpcGate.SuppressCapture = false;
                }

                if (health > 0)
                {
                    agent.Health = health;
                }
                if (currentEquipment.HasValue)
                    currentEquipment.Value.Apply(agent);
                agent.FadeIn();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[LocationSync] Build/spawn failed for character '{Name}'", character?.StringId ?? "<null>");
                agent = null;
            }
        }, blocking: true);

        return agent;
    }

    private MissionEquipmentData PackMissionEquipmentData(MissionEquipment equipment)
    {
        if (equipment == null) return null;

        var weaponSlots = new List<MissionWeaponData>();
        for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot;
             index < EquipmentIndex.NumAllWeaponSlots;
             index++)
        {
            if (!missionWeaponDataMapper.TryPack(
                    equipment[index],
                    out MissionWeaponData weapon))
            {
                return null;
            }

            weaponSlots.Add(weapon);
        }
        return new MissionEquipmentData(weaponSlots);
    }

    private MissionEquipment ResolveMissionEquipment(MissionEquipmentData data)
    {
        if (data?.WeaponSlots == null ||
            data.WeaponSlots.Count != (int)EquipmentIndex.NumAllWeaponSlots)
        {
            return null;
        }

        var equipment = new MissionEquipment();
        for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot;
             index < EquipmentIndex.NumAllWeaponSlots;
             index++)
        {
            MissionWeaponData weapon = data.WeaponSlots[(int)index];
            if (weapon == null) continue;
            if (!missionWeaponDataMapper.TryResolve(
                    weapon,
                    out MissionWeapon resolvedWeapon))
            {
                return null;
            }

            equipment._weaponSlots[(int)index] = resolvedWeapon;
        }
        return equipment;
    }

    internal static bool ShouldDisableHorses(bool hasMount) => !hasMount;

    // Cheap, non-throwing pre-filter for the common "unresolved remote hero" case, so the normal
    // path does not rely on a thrown exception (which trips first-chance break in the debugger).
    private static bool LooksUsable(CharacterObject character)
    {
        if (character == null) return false;
        try
        {
            return character.Culture != null && character.Race >= 0;
        }
        catch
        {
            return false;
        }
    }
}
