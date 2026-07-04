using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
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
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Owner-side spawn replication for a coop battle. Each client spawns only the troops it owns — its own
/// party, plus the AI/enemy side on the host — and this component captures each of those spawns
/// (<see cref="AgentSpawnedInBattle"/>, published by <c>BattleAgentSpawnedPatch</c>), registers the agent
/// under the local controller and replicates it over the mesh so peers spawn a matching puppet driven by our
/// movement. It also replays everything we own to a joiner catching up, and broadcasts our own-party troops
/// at their deployed positions on the deployment commit (requirement #4, "hidden everywhere until deployed").
/// </summary>
public interface IOwnedAgentReplicator : IDisposable
{
    /// <summary>
    /// Replay the battle agents WE own to a peer that just joined, so it spawns matching puppets driven by our
    /// movement. The per-spawn broadcasts only reached peers already connected when each agent spawned, so a
    /// player entering after troops are on the field — including a mid-battle joiner — would otherwise miss
    /// them. Each client replays the agents IT owns, so the joiner is caught up from every owner (its own
    /// troops it spawns natively).
    /// </summary>
    void ReplicateCurrentAgentsTo(string controllerId);

    /// <summary>
    /// [Owner, game thread] Replicate our own-party troops at their DEPLOYED positions so peers spawn matching
    /// puppets where we placed them. Called on the FIRST deployment commit — until then these were withheld
    /// (requirement #4: hidden everywhere until deployed). Must run synchronously on the commit's game-thread
    /// call, before the native un-pause moves the troops, so the captured positions are the deployed ones.
    /// </summary>
    void BroadcastOwnDeployedTroops();
}

/// <inheritdoc cref="IOwnedAgentReplicator"/>
public class OwnedAgentReplicator : IOwnedAgentReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<OwnedAgentReplicator>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly ICasualtyAttributionMap casualties;
    private readonly IBattleDeploymentCoordinator deployment;

    public OwnedAgentReplicator(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session,
        ICasualtyAttributionMap casualties,
        IBattleDeploymentCoordinator deployment)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.casualties = casualties;
        this.deployment = deployment;

        messageBroker.Subscribe<AgentSpawnedInBattle>(Handle_AgentSpawnedInBattle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AgentSpawnedInBattle>(Handle_AgentSpawnedInBattle);
    }

    // Reading agent transforms must run on the game thread; the send is inside the same action so the snapshot
    // and the message stay consistent. Delivery is reliable (MessagePacket -> ReliableOrdered), so the whole
    // batch fragments and arrives intact.
    public void ReplicateCurrentAgentsTo(string controllerId)
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

    public void BroadcastOwnDeployedTroops()
    {
        if (Mission.Current == null) return;

        var records = BuildOwnedAgentRecords(ownPartyOnly: true);
        if (records.Count == 0) return;

        network.SendAll(new NetworkSpawnBattleAgents(records.ToArray()));
        Logger.Information("[BattleSync] Committed deployment: broadcast {Count} own-party troop(s) at deployed positions", records.Count);
    }

    // [Game thread] Build spawn records for the battle agents WE currently own, at their CURRENT positions.
    // <paramref name="ownPartyOnly"/> limits it to the local player's own-party troops — used by the deployment
    // commit, which withholds those until they are placed; the joiner catch-up passes false to replay all we own.
    private List<BattleAgentSpawnData> BuildOwnedAgentRecords(bool ownPartyOnly)
    {
        var records = new List<BattleAgentSpawnData>();
        foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(session.OwnControllerId))
        {
            var agent = info.Agent;
            if (agent == null || !agent.IsActive() || !(agent.Character is CharacterObject character)) continue;
            if (ownPartyOnly && !IsOwnPartyAgent(agent, character)) continue;

            // Carried by CharacterObject id, uniform for heroes and troops (hero CharacterObjects are registered).
            if (!objectManager.TryGetId(character, out var characterId)) continue;

            var attribution = casualties.GetOrDefault(info.AgentId);
            var side = agent.Team != null ? agent.Team.Side : BattleSideEnum.None;
            Logger.Information("[SideDiag] ReplicateCurrentAgentsTo: agent {AgentId} team={Team} side={Side}", info.AgentId, agent.Team, side);

            records.Add(new BattleAgentSpawnData(
                info.AgentId, characterId, agent.Position, side, agent.Health,
                session.OwnControllerId, attribution.MapEventPartyId, attribution.TroopSeed));
        }
        return records;
    }

    // Whether an agent belongs to the LOCAL player's own party — the troops withheld until deployment commit
    // (requirement #4). The player's hero and the troops the local supplier spawned for MainParty are own-party;
    // the host's enemy/allied AI (a different origin party) is not, so it shows up frozen during deployment (#1).
    internal static bool IsOwnPartyAgent(Agent agent, CharacterObject character)
    {
        if (character.IsHero && character.HeroObject == Hero.MainHero) return true;
        return agent.Origin is CoopAgentOrigin origin && origin.Party == PartyBase.MainParty;
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

        string owner = session.OwnControllerId;
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
        casualties.Record(agentId, mapEventPartyId, troopSeed, characterId);

        BattleSideEnum side = agent.Team != null ? agent.Team.Side : BattleSideEnum.None;
        Logger.Information("[SideDiag] Handle_AgentSpawnedInBattle: agent {AgentId} team={Team} side={Side}", agentId, agent.Team, side);
        var data = new BattleAgentSpawnData(agentId, characterId, agent.Position, side, agent.Health, owner, mapEventPartyId, troopSeed);

        // Requirement #4 "hidden everywhere until deployed": while we are still placing our own formations our
        // own-party troops are spawned locally (so we can deploy them) but NOT replicated, so other clients never
        // see them mid-deployment. They are broadcast at their deployed positions when we commit (see
        // BroadcastOwnDeployedTroops). NPC/AI agents WE own (the host's enemy side) are not withheld —
        // they must show up frozen on every client during deployment (requirement #1).
        if (deployment.ShouldWithhold(IsOwnPartyAgent(agent, character)))
        {
            Logger.Information("[BattleSync] Withholding own spawn {Char} (agent {AgentId}) until deployment commit", characterId, agentId);
            return;
        }

        // SendAll over the mesh reaches every peer in this battle instance (not us).
        Logger.Information("[BattleSync] Captured own spawn {Char} (agent {AgentId}); broadcasting over mesh", characterId, agentId);
        network.SendAll(new NetworkSpawnBattleAgents(new[] { data }));
    }

    private static void AttachPlayerAgent(Agent agent, CharacterObject character)
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
}
