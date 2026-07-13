using Common;
using Common.Logging;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Tournaments.Data;
using Missions.Data;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments.Spectators;

public interface ITournamentSpectatorAgentManager
{
    void Reconcile(TournamentSessionSnapshot snapshot);
    void Tick();
    void HandleJoinInfo(NetworkMissionJoinInfo joinInfo);
    CoopAgentSpawnData[] GetLocalSpawnData();
    string LastLocalSpawnName { get; }
    bool IsSpectatorAgent(Agent agent);
    bool IsOrange(ItemObject item);
    void Clear();
}

public interface ITournamentSpectatorAgentManagerFactory
{
    ITournamentSpectatorAgentManager Create(ICoopMissionComponent coopMissionComponent);
}

public class TournamentSpectatorAgentManagerFactory : ITournamentSpectatorAgentManagerFactory
{
    private readonly IBattleNetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;

    public TournamentSpectatorAgentManagerFactory(
        IBattleNetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IControllerIdProvider controllerIdProvider)
    {
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;
    }

    public ITournamentSpectatorAgentManager Create(ICoopMissionComponent coopMissionComponent)
        => new TournamentSpectatorAgentManager(
            network,
            objectManager,
            playerManager,
            controllerIdProvider,
            coopMissionComponent);
}

public class TournamentSpectatorAgentManager : ITournamentSpectatorAgentManager
{
    private static readonly ILogger Logger = LogManager.GetLogger<TournamentSpectatorAgentManager>();
    private const float SpawnPositionToleranceSquared = 0.01f;

    private readonly IBattleNetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly ConcurrentQueue<NetworkMissionJoinInfo> pendingJoinInfos = new();
    private readonly Dictionary<string, SpectatorAgentRecord> spectators = new();
    private TournamentSessionSnapshot snapshot;
    private ItemObject orangeItem;

    public string LastLocalSpawnName { get; private set; }

    public TournamentSpectatorAgentManager(
        IBattleNetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IControllerIdProvider controllerIdProvider,
        ICoopMissionComponent coopMissionComponent)
    {
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;
        this.coopMissionComponent = coopMissionComponent;
    }

    public void Reconcile(TournamentSessionSnapshot updated)
    {
        snapshot = updated;
        GameThread.RunSafe(ReconcileOnGameThread, blocking: true);
    }

    public void Tick()
    {
        string controllerId = controllerIdProvider.ControllerId;
        if (!spectators.TryGetValue(controllerId, out var record)) return;
        Agent agent = record.Agent;
        if (agent == null || !agent.IsActive() || !TryResolveOrangeItem(out ItemObject item)) return;

        MissionWeapon weapon = agent.Equipment[TournamentSpectatorOrange.EquipmentSlot];
        if (!TournamentSpectatorOrange.ShouldRefill(weapon.Item, item, weapon.Amount)) return;
        agent.SetWeaponAmountInSlot(
            TournamentSpectatorOrange.EquipmentSlot,
            TournamentSpectatorOrange.RefillAmount,
            false);
    }

    public void HandleJoinInfo(NetworkMissionJoinInfo joinInfo)
    {
        if (joinInfo == null) return;
        if (snapshot == null)
        {
            pendingJoinInfos.Enqueue(joinInfo);
            return;
        }

        GameThread.RunSafe(() => ProcessJoinInfo(joinInfo));
    }

    public CoopAgentSpawnData[] GetLocalSpawnData()
    {
        CoopAgentSpawnData[] result = Array.Empty<CoopAgentSpawnData>();
        GameThread.RunSafe(() => result = BuildLocalSpawnData(), blocking: true);
        return result;
    }

    private CoopAgentSpawnData[] BuildLocalSpawnData()
    {
        string controllerId = controllerIdProvider.ControllerId;
        if (!spectators.TryGetValue(controllerId, out var record) ||
            record.Agent == null || !record.Agent.IsActive())
            return Array.Empty<CoopAgentSpawnData>();

        return new[]
        {
            new CoopAgentSpawnData(
                record.AgentId,
                record.CharacterId,
                record.Spawn.Position,
                record.Agent.Health,
                true)
        };
    }

    public bool IsSpectatorAgent(Agent agent)
        => agent != null && spectators.Values.Any(record => ReferenceEquals(record.Agent, agent));

    public bool IsOrange(ItemObject item)
        => item != null && TryResolveOrangeItem(out ItemObject resolved) && ReferenceEquals(item, resolved);

    public void Clear()
    {
        GameThread.RunSafe(ClearOnGameThread, blocking: true);
    }

    public static string[] GetEligibleControllers(TournamentSessionSnapshot state)
    {
        if (state?.Phase != TournamentSessionPhase.LiveMatch) return Array.Empty<string>();

        var entrants = new HashSet<string>(state.SuccessorControllerIds ?? Array.Empty<string>());
        if (!string.IsNullOrEmpty(state.HostControllerId)) entrants.Add(state.HostControllerId);

        var fighters = new HashSet<string>();
        TournamentMatchData match = state.Rounds?
            .Where(round => round?.Matches != null)
            .SelectMany(round => round.Matches)
            .FirstOrDefault(candidate => candidate?.MatchId == state.CurrentMatchId);
        if (match != null)
        {
            var fightingSlots = new HashSet<string>(match.Teams
                .Where(team => team?.ParticipantSlotIds != null)
                .SelectMany(team => team.ParticipantSlotIds));
            foreach (TournamentContestantData contestant in state.Contestants)
                if (contestant != null && fightingSlots.Contains(contestant.SlotId))
                    fighters.Add(contestant.ControllerId);
        }

        var eligible = new HashSet<string>(state.SpectatorControllerIds ?? Array.Empty<string>());
        foreach (TournamentContestantData contestant in state.Contestants ?? Array.Empty<TournamentContestantData>())
        {
            if (contestant == null || !contestant.IsHuman || contestant.IsReplaced) continue;
            if (!fighters.Contains(contestant.ControllerId)) eligible.Add(contestant.ControllerId);
        }

        eligible.IntersectWith(entrants);
        eligible.RemoveWhere(string.IsNullOrEmpty);
        return eligible.OrderBy(controllerId => controllerId, StringComparer.Ordinal).ToArray();
    }

    private void ReconcileOnGameThread()
    {
        if (Mission.Current == null ||
            !TournamentSpectatorSceneLayouts.TryGet(Mission.Current.SceneName, out var layout) ||
            snapshot?.Phase != TournamentSessionPhase.LiveMatch)
        {
            ClearOnGameThread();
            DrainPendingJoinInfos();
            return;
        }

        var eligible = new HashSet<string>(GetEligibleControllers(snapshot));
        foreach (string controllerId in spectators.Keys.Where(id => !eligible.Contains(id)).ToArray())
            RemoveSpectator(controllerId);

        string ownControllerId = controllerIdProvider.ControllerId;
        if (eligible.Contains(ownControllerId) && !spectators.ContainsKey(ownControllerId))
            SpawnLocalSpectator(layout);

        DrainPendingJoinInfos();
    }

    private void SpawnLocalSpectator(TournamentSpectatorSceneLayout layout)
    {
        string controllerId = controllerIdProvider.ControllerId;
        if (!TryResolveCharacter(controllerId, out string characterId, out CharacterObject character)) return;
        if (layout.Spawns.Count == 0) return;

        TournamentSpectatorSpawnData spawn = layout.Spawns[MBRandom.RandomInt(layout.Spawns.Count)];
        Guid agentId = Guid.NewGuid();
        Agent agent = SpawnAgent(character, spawn, AgentControllerType.Player);
        if (agent == null) return;
        if (!coopMissionComponent.AgentRegistry.TryRegisterAgent(controllerId, agentId, agent))
        {
            agent.FadeOut(false, true);
            return;
        }

        spectators[controllerId] = new SpectatorAgentRecord(agentId, characterId, spawn, agent);
        LastLocalSpawnName = spawn.Name;
        Logger.Information(
            "[TournamentSpectator] Spawned local spectator {ControllerId} at {SpawnName}: position=({X}, {Y}, {Z})",
            controllerId,
            spawn.Name,
            spawn.Position.x,
            spawn.Position.y,
            spawn.Position.z);
        Mission.Current.MainAgent = agent;
        network.SendAll(new NetworkMissionJoinInfo(controllerId, true, BuildLocalSpawnData()));
    }

    private void DrainPendingJoinInfos()
    {
        while (pendingJoinInfos.TryDequeue(out var joinInfo))
            ProcessJoinInfo(joinInfo);
    }

    private void ProcessJoinInfo(NetworkMissionJoinInfo joinInfo)
    {
        if (Mission.Current == null || snapshot?.Phase != TournamentSessionPhase.LiveMatch) return;
        if (joinInfo.ControllerId == controllerIdProvider.ControllerId) return;
        if (!GetEligibleControllers(snapshot).Contains(joinInfo.ControllerId)) return;
        if (!TournamentSpectatorSceneLayouts.TryGet(Mission.Current.SceneName, out var layout)) return;
        if (!TryResolveCharacter(joinInfo.ControllerId, out string characterId, out CharacterObject character)) return;

        CoopAgentSpawnData[] spawnData = joinInfo.AiAgentData ?? Array.Empty<CoopAgentSpawnData>();
        if (spawnData.Length != 1) return;
        CoopAgentSpawnData data = spawnData[0];
        if (data == null || data.AgentId == Guid.Empty || data.CharacterObjectId != characterId) return;
        if (!TryResolveSpawn(layout, data.Position, out var spawn)) return;
        if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(data.AgentId, out _)) return;

        RemoveSpectator(joinInfo.ControllerId);
        Agent agent = SpawnAgent(character, spawn, AgentControllerType.None);
        if (agent == null) return;
        if (!coopMissionComponent.AgentRegistry.TryRegisterAgent(joinInfo.ControllerId, data.AgentId, agent))
        {
            agent.FadeOut(false, true);
            return;
        }

        spectators[joinInfo.ControllerId] = new SpectatorAgentRecord(
            data.AgentId,
            characterId,
            spawn,
            agent);
        Logger.Information(
            "[TournamentSpectator] Spawned remote spectator {ControllerId} at {SpawnName}",
            joinInfo.ControllerId,
            spawn.Name);
    }

    private bool TryResolveOrangeItem(out ItemObject item)
    {
        if (orangeItem != null)
        {
            item = orangeItem;
            return true;
        }

        if (!objectManager.TryGetObject(TournamentSpectatorOrange.ItemId, out orangeItem))
        {
            item = null;
            return false;
        }

        item = orangeItem;
        return true;
    }

    private bool TryResolveCharacter(
        string controllerId,
        out string characterId,
        out CharacterObject character)
    {
        characterId = null;
        character = null;
        if (!playerManager.TryGetPlayer(controllerId, out Player player)) return false;
        characterId = player.CharacterObjectId;
        return !string.IsNullOrEmpty(characterId) && objectManager.TryGetObject(characterId, out character);
    }

    private static bool TryResolveSpawn(
        TournamentSpectatorSceneLayout layout,
        Vec3 position,
        out TournamentSpectatorSpawnData spawn)
    {
        foreach (TournamentSpectatorSpawnData candidate in layout.Spawns)
        {
            if ((candidate.Position - position).LengthSquared > SpawnPositionToleranceSquared) continue;
            spawn = candidate;
            return true;
        }

        spawn = default;
        return false;
    }

    private Agent SpawnAgent(
        CharacterObject character,
        TournamentSpectatorSpawnData spawn,
        AgentControllerType controller)
    {
        try
        {
            MatrixFrame frame = MatrixFrame.Identity;
            frame.rotation.RotateAboutUp(spawn.Rotation);
            bool hasOrange = TryResolveOrangeItem(out ItemObject item);
            if (!hasOrange)
                Logger.Warning(
                    "[TournamentSpectator] Could not resolve spectator orange item {ItemId}; spawning unarmed",
                    TournamentSpectatorOrange.ItemId);
            Equipment equipment = TournamentSpectatorOrange.BuildEquipment(
                character.FirstCivilianEquipment,
                hasOrange ? item : null);
            var buildData = new AgentBuildData(character)
                .TroopOrigin(new SimpleAgentOrigin(character, -1, null, default))
                .BodyProperties(character.GetBodyPropertiesMax())
                .InitialPosition(spawn.Position)
                .InitialDirection(frame.rotation.f.AsVec2)
                .CanSpawnOutsideOfMissionBoundary(true)
                .Equipment(equipment)
                .NoHorses(true)
                .NoWeapons(false)
                .Controller(controller);

            if (character.HeroObject?.MapFaction != null)
            {
                buildData.ClothingColor1(character.HeroObject.MapFaction.Color);
                buildData.ClothingColor2(character.HeroObject.MapFaction.Color2);
            }

            Agent agent = Mission.Current.SpawnAgent(buildData);
            agent.SetWatchState(Agent.WatchState.Alarmed);
            if (hasOrange)
            {
                agent.TryToWieldWeaponInSlot(
                    TournamentSpectatorOrange.EquipmentSlot,
                    Agent.WeaponWieldActionType.InstantAfterPickUp,
                    false);
                MissionWeapon equippedOrange = agent.Equipment[TournamentSpectatorOrange.EquipmentSlot];
                Logger.Information(
                    "[TournamentSpectator] Equipped spectator orange in {EquipmentSlot}: item={ItemId}, amount={Amount}",
                    TournamentSpectatorOrange.EquipmentSlot,
                    equippedOrange.Item?.StringId,
                    equippedOrange.Amount);
            }
            agent.FadeIn();
            return agent;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[TournamentSpectator] Could not spawn spectator character {CharacterId}", character?.StringId);
            return null;
        }
    }

    private void ClearOnGameThread()
    {
        foreach (string controllerId in spectators.Keys.ToArray())
            RemoveSpectator(controllerId);
    }

    private void RemoveSpectator(string controllerId)
    {
        if (!spectators.TryGetValue(controllerId, out var record)) return;
        spectators.Remove(controllerId);

        Agent agent = record.Agent;
        if (Mission.Current?.MainAgent == agent)
            Mission.Current.MainAgent = null;
        coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent);
        coopMissionComponent.AgentRegistry.RemoveAgent(record.AgentId);
        if (agent == null || !agent.IsActive()) return;

        agent.Controller = AgentControllerType.None;
        agent.FadeOut(false, true);
    }

    private sealed class SpectatorAgentRecord
    {
        public readonly Guid AgentId;
        public readonly string CharacterId;
        public readonly TournamentSpectatorSpawnData Spawn;
        public readonly Agent Agent;

        public SpectatorAgentRecord(
            Guid agentId,
            string characterId,
            TournamentSpectatorSpawnData spawn,
            Agent agent)
        {
            AgentId = agentId;
            CharacterId = characterId;
            Spawn = spawn;
            Agent = agent;
        }
    }
}
