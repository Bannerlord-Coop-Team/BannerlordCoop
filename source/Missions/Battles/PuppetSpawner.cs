using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Missions.Data;
using GameInterface.Services.Players;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Peer-side spawn application for a coop battle: spawns the agents other owners replicate over the mesh
/// (<see cref="NetworkSpawnBattleAgents"/>) as local puppets driven by their owner's movement. Spawns that
/// arrive before their team or explicit party identity exists are buffered and drained on tick. During local
/// deployment, only this client's own withheld records wait for commit; remote NPCs are visible and frozen.
/// </summary>
public interface IPuppetSpawner : IDisposable
{
    /// <summary>
    /// [Game thread] Drain puppets whose teams and party identities now exist, retaining local records until commit.
    /// </summary>
    void DrainPendingPuppets();
}

/// <inheritdoc cref="IPuppetSpawner"/>
public class PuppetSpawner : IPuppetSpawner
{
    private static readonly ILogger Logger = LogManager.GetLogger<PuppetSpawner>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly ICasualtyAttributionMap casualties;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IAgentFormationAssigner formationAssigner;

    // Spawn records can arrive before their mission team or world-stream party. Buffer them until both exist;
    // agents without that identity later break team ownership and scoreboard attribution.
    private readonly object pendingPuppetLock = new object();
    private readonly List<BattleAgentSpawnData> pendingPuppets = new List<BattleAgentSpawnData>();
    private readonly object withdrawnControllerLock = new object();
    private readonly HashSet<string> withdrawnControllers = new HashSet<string>();
    private readonly HashSet<string> withdrawnHostControllers = new HashSet<string>();

    public PuppetSpawner(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session,
        ICasualtyAttributionMap casualties,
        IBattleDeploymentCoordinator deployment,
        IAgentFormationAssigner formationAssigner)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.casualties = casualties;
        this.deployment = deployment;
        this.formationAssigner = formationAssigner;

        messageBroker.Subscribe<NetworkSpawnBattleAgents>(Handle_NetworkSpawnBattleAgents);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSpawnBattleAgents>(Handle_NetworkSpawnBattleAgents);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
    }

    // [Peer] Spawn the owner's agents as local puppets, driven by replicated movement.
    private void Handle_NetworkSpawnBattleAgents(MessagePayload<NetworkSpawnBattleAgents> payload)
    {
        if (payload.What.Agents == null) return;

        Logger.Information("[BattleSync] Received {Count} spawn record(s) from the host over the mesh", payload.What.Agents.Length);
        foreach (var data in payload.What.Agents)
            SpawnPuppet(data);
    }

    private void SpawnPuppet(BattleAgentSpawnData data)
    {
        if (data.AgentId == Guid.Empty) return;

        // Spawn on the game thread, but do NOT block the network (receive) thread: while the mission is still
        // loading the game loop isn't draining the GameThread queue, so a blocking wait here deadlocks the
        // receive thread. Buffer missing mission identity and local pre-commit records for the tick drain.
        GameThread.RunSafe(() =>
        {
            if (!TrySpawnPuppetNow(data))
                lock (pendingPuppetLock) pendingPuppets.Add(data);
        });
    }

    // [Game thread] Spawn one puppet. Returns false when a required team or explicit party identity is pending.
    private bool TrySpawnPuppetNow(BattleAgentSpawnData data)
    {
        var registry = coopMissionComponent.AgentRegistry;

        if (Mission.Current == null) return true;                       // no mission — drop
        if (IsWithdrawnPlayerParty(data)) return true;                  // stale replay after leave/drop — drop
        if (registry.TryGetAgentInfo(data.AgentId, out _)) return true; // already spawned — dedupe

        // Keep only our own withheld deployment records out of the mission until commit. Remote NPCs must remain
        // visible (frozen) during deployment; the foreign-team deployment patch keeps their plan-less team from
        // stalling this client's own spawn gate.
        bool isOwnAgent = session.IsOwn(data.OwnerControllerId);
        if (isOwnAgent && LocalDeploymentInProgress()) return false;

        var team = ResolvePuppetTeam(data);
        if (team == null) return false;                                 // teams not created yet — buffer

        if (!objectManager.TryGetObjectWithLogging(data.CharacterId, out CharacterObject character))
        {
            Logger.Warning("[BattleSync] Puppet skipped: unresolved character {Char} for agent {AgentId}", data.CharacterId, data.AgentId);
            return true;
        }

        // We own the agent when the record's assignment owner is us. This can be our initial spawn record or a
        // fresh record after re-entry. Our hero becomes the local main agent; our troops become locally driven
        // AI combatants. Everything else is an inert puppet driven by its owner over the mesh.
        bool isOwnHero = isOwnAgent && character.IsHero && character.HeroObject == Hero.MainHero;

        // Carry the troop's party so the agent has a real BattleCombatant — the battle observer/scoreboard
        // reads origin.BattleCombatant, and SimpleAgentOrigin leaves it null for non-hero troops.
        var party = ResolvePuppetParty(data.MapEventPartyId);

        if (party == null)
        {
            // World-state registration and mission-mesh spawns use different channels. An explicit party id
            // can therefore arrive first; retain it until the game-thread registry apply catches up.
            if (!string.IsNullOrEmpty(data.MapEventPartyId)) return false;

            // An unattributed spawn record must still produce a body — a puppet that never spawns is an
            // invisible enemy (and re-buffering forever spams the log every tick). Fall back to any
            // involved party on the agent's side; only the scoreboard attribution degrades.
            party = ResolveFallbackParty(data.Side);
            if (party == null)
            {
                Logger.Warning("[BattleSync] Puppet skipped: unresolved party {Party} for agent {AgentId}", data.MapEventPartyId, data.AgentId);
                return false;
            }

            Logger.Warning("[BattleSync] Puppet {AgentId} spawned with a fallback {Side} party; {Party} unresolved", data.AgentId, data.Side, data.MapEventPartyId);
        }

        var origin = new CoopAgentOrigin(character, party, -1, null, new UniqueTroopDescriptor(data.TroopSeed));

        var missionEquipment = ResolveMissionEquipment(data.MissionEquipmentData);

        var buildData = new AgentBuildData(character);
        buildData.InitialPosition(data.Position);
        buildData.Team(team);
        buildData.InitialDirection(Vec2.Forward);
        buildData.Equipment(data.SpawnEquipment); // Use calculated equipment from spawning client instead of character equipment (random per troop per client)
        buildData.BodyProperties(data.BodyProperties);
        buildData.Banner(origin.Banner);
        buildData.TroopOrigin(origin);
        buildData.MissionEquipment(missionEquipment);
        buildData.Controller(isOwnHero ? AgentControllerType.Player
            : isOwnAgent ? AgentControllerType.AI
            : AgentControllerType.None);
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

        formationAssigner.Assign(agent, data.FormationIndex);

        // Adopt our own hero as the controllable main agent of this mission.
        if (isOwnHero)
        {
            Mission.Current.MainAgent = agent;
        }
        else if (isOwnAgent)
        {
            // One of our own troops arriving as a spawn record. It spawned AI-controlled and locally driven
            // (never an interpolated puppet — we are its authority the moment it registers below), so wake its
            // AI exactly as the adopt/reinforcement paths do or it stands idle. If our
            // hero died while we were gone, the leaderless-control path (ChargeLeaderlessOwnTroops) charges
            // these formations at our deployment finish, exactly as it does for a fresh leaderless spawn.
            AgentAiWaker.Wake(agent);
        }
        else
        {
            // Keep the puppet un-paused so it follows its owner's movement even while THIS client is still in its
            // own deployment freeze (native deployment sets Mission.AllowAiTicking=false and AI-pauses agents). A
            // mid-battle joiner spawns these puppets while deploying into an ALREADY-LIVE battle; left paused, the
            // puppet never walks the small per-tick deltas its owner sends (AgentData.Apply only teleports on >1u
            // jumps), so the whole live battle looks frozen until the joiner clicks Start Battle. Mirrors the
            // adopt and reinforcement paths, which un-pause too.
            agent.SetIsAIPaused(false);
        }

        registry.TryRegisterAgent(data.OwnerControllerId, data.AgentId, agent);

        // The owner registered its cavalry's horse with its own network id; our engine spawned a matching
        // horse implicitly (same equipment) inside SpawnAgent. Register OUR copy under the same id, so mount
        // hits route by the horse's identity and its death broadcast finds it. No casualty record — a horse
        // is not a roster troop. If the puppet unexpectedly spawned on foot, the id just stays unmapped here
        // and hits on the (nonexistent) horse can't occur anyway.
        if (data.MountAgentId != Guid.Empty)
        {
            if (agent.MountAgent is Agent mount)
                registry.TryRegisterAgent(data.OwnerControllerId, data.MountAgentId, mount);
            else
                Logger.Warning("[BattleSync] Spawn record for {AgentId} carries mount {MountId} but the puppet spawned unmounted", data.AgentId, data.MountAgentId);
        }

        // Key the casualty on the troop's CHARACTER through the object manager (never a raw StringId).
        objectManager.TryGetId(character, out var troopCharacterId);
        casualties.Record(data.AgentId, data.MapEventPartyId, data.TroopSeed, troopCharacterId);
        Logger.Information("[BattleSync] Spawned puppet {Char} (agent {AgentId}, ownAgent={Own})", data.CharacterId, data.AgentId, isOwnAgent);
        return true;
    }

    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        if (payload.What.InstanceId != null && payload.What.InstanceId != session.InstanceId) return;
        GameThread.RunSafe(() =>
        {
            lock (withdrawnControllerLock)
            {
                withdrawnControllers.Remove(payload.What.ControllerId);
                withdrawnHostControllers.Remove(payload.What.ControllerId);
            }
        });
    }

    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        MarkControllerWithdrawn(payload.What.ControllerId, payload.What.InstanceId);
    }

    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        MarkControllerWithdrawn(payload.What.ControllerId, payload.What.InstanceId);
    }

    private void MarkControllerWithdrawn(string controllerId, string instanceId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (instanceId != null && instanceId != session.InstanceId) return;
        bool wasHost = session.IsHostController(controllerId);
        lock (withdrawnControllerLock)
        {
            withdrawnControllers.Add(controllerId);
            if (wasHost) withdrawnHostControllers.Add(controllerId);
        }

        // Records received before the departure may already be sitting in the deployment buffer. Remove the
        // player's party on the game thread, while leaving NPC parties from the old host available to migrate.
        GameThread.RunSafe(() =>
        {
            lock (pendingPuppetLock)
                pendingPuppets.RemoveAll(data => data.OwnerControllerId == controllerId
                    && (!wasHost || IsPlayerPartyRecord(data, controllerId)));
        });
    }

    // [Game thread] A replay from the old host can already be buffered when its disconnect arrives. Drop only
    // records for that player's own party; NPC parties the host ran still belong to the successor migration.
    private bool IsWithdrawnPlayerParty(BattleAgentSpawnData data)
    {
        bool wasHost;
        lock (withdrawnControllerLock)
        {
            if (!withdrawnControllers.Contains(data.OwnerControllerId)) return false;
            wasHost = withdrawnHostControllers.Contains(data.OwnerControllerId);
        }

        return !wasHost || IsPlayerPartyRecord(data, data.OwnerControllerId);
    }

    // [Game thread] Match a spawn record to the controller's player party. The hero check covers a record
    // whose MapEventParty id was unavailable when it was captured.
    private bool IsPlayerPartyRecord(BattleAgentSpawnData data, string controllerId)
    {
        if (!playerManager.TryGetPlayer(controllerId, out var player)) return false;

        if (data.MapEventPartyId != null
            && objectManager.TryGetObject<MapEventParty>(data.MapEventPartyId, out var mapEventParty)
            && objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var mobileParty)
            && mapEventParty?.Party == mobileParty?.Party)
        {
            return true;
        }

        return objectManager.TryGetObject<Hero>(player.HeroId, out var hero)
            && objectManager.TryGetObject<CharacterObject>(data.CharacterId, out var character)
            && character.IsHero
            && character.HeroObject == hero;
    }

    // True while THIS client is still in its own Order-of-Battle deployment. Only locally owned records use this
    // gate; remote NPC puppets may populate foreign teams because the deployment patch treats those as planned.
    private bool LocalDeploymentInProgress()
        => !deployment.IsCommitted
           && Mission.Current?.GetMissionBehavior<DeploymentMissionController>() != null;

    public void DrainPendingPuppets()
    {
        if (Mission.Current == null || Mission.Current.DefenderTeam == null) return;

        BattleAgentSpawnData[] pending;
        lock (pendingPuppetLock)
        {
            if (pendingPuppets.Count == 0) return;
            pending = pendingPuppets.ToArray();
            pendingPuppets.Clear();
        }

        foreach (var data in pending)
        {
            // Per-puppet guard: one bad record must not abort the whole drain (and re-throw every tick). On
            // failure, drop it rather than re-buffering, so it can't spin a per-tick exception loop.
            try
            {
                if (!TrySpawnPuppetNow(data))
                    lock (pendingPuppetLock) pendingPuppets.Add(data);
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

    // Any involved party of the given side, for a spawn record whose own party never resolved here.
    private static PartyBase ResolveFallbackParty(BattleSideEnum side)
    {
        var mapEvent = MobileParty.MainParty?.MapEvent;
        if (mapEvent == null || side == BattleSideEnum.None) return null;

        foreach (var involved in mapEvent.GetMapEventSide(side).Parties)
        {
            if (involved?.Party != null) return involved.Party;
        }

        return null;
    }

    // Another owner's puppet must stay off PlayerTeam because every formation there is locally commandable.
    // Use the side's non-player team; missing main or ally teams are buffered until initialization completes.
    private Team ResolvePuppetTeam(BattleAgentSpawnData data)
    {
        var mainTeam = BattleTeams.Resolve(data.Side);
        if (mainTeam == null) return null;

        // Our OWN troop replicated back to us (e.g. our own-party deployment broadcast echoed over the mesh) belongs
        // on our own team — it is the one puppet we DO control.
        if (session.IsOwn(data.OwnerControllerId))
            return mainTeam;

        var playerTeam = Mission.Current.PlayerTeam;
        if (mainTeam != playerTeam) return mainTeam;          // main team isn't ours (we're an ally) — safe to use

        // The side's main team IS our PlayerTeam, so route to the side's ally team instead so we can't command it.
        var allyTeam = data.Side == BattleSideEnum.Attacker
            ? Mission.Current.AttackerAllyTeam
            : Mission.Current.DefenderAllyTeam;
        if (allyTeam != null && allyTeam != playerTeam) return allyTeam;

        // Never put another player's party on the local command team.
        return null;
    }

    private MissionEquipment ResolveMissionEquipment(MissionEquipmentData data)
    {
        var missionEquipment = new MissionEquipment();
        if (data == null || data.WeaponSlots.Count == 0) return null;

        for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.NumAllWeaponSlots; equipmentIndex++)
        {
            missionEquipment._weaponSlots[(int)equipmentIndex] = ResolveMissionWeapon(data.WeaponSlots[(int)equipmentIndex]);
        }
        return missionEquipment;
    }

    private MissionWeapon ResolveMissionWeapon(MissionWeaponData data)
    {
        // Items can be null
        objectManager.TryGetObject<ItemObject>(data.ItemObjectId, out var item);

        var missionWeapon = new MissionWeapon(item, data.ItemModifier, data.Banner, data.DataValue, data.ReloadPhase, null);
        return missionWeapon;
    }
}
