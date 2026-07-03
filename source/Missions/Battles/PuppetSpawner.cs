using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
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
/// arrive while this client's mission is still loading (no teams yet) or while it is still in its own
/// deployment are buffered and drained on tick, so an agent is never built team-less (which later NREs the
/// scoreboard) and never populates a team the native deployment spawn gate inspects.
/// </summary>
public interface IPuppetSpawner : IDisposable
{
    /// <summary>
    /// [Game thread] Drain puppets buffered while the mission's teams did not yet exist (or while we were
    /// still deploying), once they do / once our deployment has committed.
    /// </summary>
    void DrainPendingPuppets();
}

/// <inheritdoc cref="IPuppetSpawner"/>
public class PuppetSpawner : IPuppetSpawner
{
    private static readonly ILogger Logger = LogManager.GetLogger<PuppetSpawner>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly ICasualtyAttributionMap casualties;
    private readonly IBattleDeploymentCoordinator deployment;

    // Puppet spawns from the host's catch-up burst can arrive while THIS client's mission is still loading
    // (before MissionCombatantsLogic creates the teams). An agent built with a null team later NREs the
    // scoreboard (BattleObserverMissionLogic.SetObserver reads agent.Team.Side from its build cache), so buffer
    // such spawns and drain them on tick once the teams exist.
    private readonly object pendingPuppetLock = new object();
    private readonly List<BattleAgentSpawnData> pendingPuppets = new List<BattleAgentSpawnData>();

    public PuppetSpawner(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session,
        ICasualtyAttributionMap casualties,
        IBattleDeploymentCoordinator deployment)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.casualties = casualties;
        this.deployment = deployment;

        messageBroker.Subscribe<NetworkSpawnBattleAgents>(Handle_NetworkSpawnBattleAgents);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSpawnBattleAgents>(Handle_NetworkSpawnBattleAgents);
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
        // receive thread. If the mission's teams don't exist yet (a catch-up burst arriving mid-load), buffer
        // and retry on tick — an agent built before its team exists is team-less and later NREs the scoreboard.
        GameThread.RunSafe(() =>
        {
            if (!TrySpawnPuppetNow(data))
                lock (pendingPuppetLock) pendingPuppets.Add(data);
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

        if (!objectManager.TryGetObjectWithLogging(data.CharacterId, out CharacterObject character))
        {
            Logger.Warning("[BattleSync] Puppet skipped: unresolved character {Char} for agent {AgentId}", data.CharacterId, data.AgentId);
            return true;
        }

        // We own the agent when we are its owner — i.e. our own hero. That hero is adopted as the local main
        // agent and player-controlled; everything else is an inert puppet driven by its owner over the mesh.
        bool isOwnAgent = session.IsOwn(data.OwnerControllerId);
        var equipment = character.IsHero ? character.HeroObject.BattleEquipment : character.Equipment;

        // Carry the troop's party so the agent has a real BattleCombatant — the battle observer/scoreboard
        // reads origin.BattleCombatant, and SimpleAgentOrigin leaves it null for non-hero troops.
        var party = ResolvePuppetParty(data.MapEventPartyId);

        if (party == null)
        {
            Logger.Warning("[BattleSync] Puppet skipped: unresolved party {Party} for agent {AgentId}", data.MapEventPartyId, data.AgentId);
            return false;
        }

        var origin = new CoopAgentOrigin(character, party, -1, null, new UniqueTroopDescriptor(data.TroopSeed));

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
            // adopt and reinforcement paths, which un-pause too.
            agent.SetIsAIPaused(false);
        }

        registry.TryRegisterAgent(data.OwnerControllerId, data.AgentId, agent);
        // Key the casualty on the troop's CHARACTER through the object manager (never a raw StringId).
        objectManager.TryGetId(character, out var troopCharacterId);
        casualties.Record(data.AgentId, data.MapEventPartyId, data.TroopSeed, troopCharacterId);
        Logger.Information("[BattleSync] Spawned puppet {Char} (agent {AgentId}, ownAgent={Own})", data.CharacterId, data.AgentId, isOwnAgent);
        return true;
    }

    // True while THIS client is still in its own Order-of-Battle deployment — a deployment controller is attached
    // and we have not committed yet. Puppets are held until commit so they don't populate (plan-less) the teams the
    // native spawn gate inspects, which would stop this client's own troops from spawning. The deployment-controller
    // check keeps this false in the headless harness (no native controller there), so puppet tests are unaffected.
    private bool LocalDeploymentInProgress()
        => !deployment.IsCommitted
           && Mission.Current?.GetMissionBehavior<DeploymentMissionController>() != null;

    public void DrainPendingPuppets()
    {
        if (Mission.Current == null || Mission.Current.DefenderTeam == null) return;
        if (LocalDeploymentInProgress()) return; // hold puppets until our own deployment commits

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

    // The team a puppet joins. A puppet is ANOTHER owner's troop, so it must NOT land on our local PlayerTeam — the
    // Order-of-Battle deployment lets the local player arrange/command EVERY formation on PlayerTeam, so a puppet
    // there becomes deployable by us (the "non-host can deploy the host hero and NPC heroes" bug). Each client only
    // spawns its OWN party into PlayerTeam (the rest arrive here as puppets), so keeping puppets off PlayerTeam means
    // the local player only ever commands its own party. We put a puppet on a NON-player team for its side: the
    // side's main team if that isn't ours, otherwise the side's ally team. Returns null only while the side's main
    // team doesn't exist yet (mission still loading) so the caller buffers and retries.
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

        // No separate ally team on our side yet (only our own party present) — fall back to the main team.
        return mainTeam;
    }
}
