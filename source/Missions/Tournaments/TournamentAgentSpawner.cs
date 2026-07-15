using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments.Data;
using Missions.Battles;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments;

public class TournamentAgentSpawner
{
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly Dictionary<string, Team> teams = new();

    public TournamentAgentSpawner(
        IObjectManager objectManager,
        IControllerIdProvider controllerIdProvider,
        ICoopMissionComponent coopMissionComponent)
    {
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;
        this.coopMissionComponent = coopMissionComponent;
    }

    public void ApplyManifest(
        TournamentSpawnManifestData manifest,
        TournamentSessionSnapshot snapshot,
        ITournamentMissionSession session)
    {
        GameThread.RunSafe(() => ApplyOnGameThread(manifest, snapshot, session), blocking: true);
    }

    public void Reset()
    {
        if (Mission.Current != null)
            foreach (Team team in teams.Values)
                Mission.Current.Teams.Remove(team);
        teams.Clear();
    }

    private void ApplyOnGameThread(
        TournamentSpawnManifestData manifest,
        TournamentSessionSnapshot snapshot,
        ITournamentMissionSession session)
    {
        if (manifest?.Agents == null || snapshot?.Contestants == null ||
            manifest.Agents.Any(data => data == null) || snapshot.Contestants.Any(data => data == null)) return;
        if (Mission.Current == null) return;
        if (manifest.Agents.All(data =>
            coopMissionComponent.AgentRegistry.TryGetAgentInfo(data.AgentId, out _)))
            return;

        EnsureTeams(manifest.Agents);
        var contestants = snapshot.Contestants.ToDictionary(data => data.SlotId);
        foreach (TournamentAgentSpawnData data in manifest.Agents)
        {
            if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(data.AgentId, out _)) continue;
            if (!contestants.TryGetValue(data.SlotId, out var contestant)) continue;
            Spawn(data, contestant, session);
        }
    }

    private void EnsureTeams(TournamentAgentSpawnData[] agents)
    {
        foreach (TournamentAgentSpawnData data in agents)
        {
            if (teams.ContainsKey(data.TeamId)) continue;

            Banner banner = string.IsNullOrEmpty(data.TeamBannerCode)
                ? Banner.CreateOneColoredEmptyBanner(unchecked((int)data.TeamColor))
                : new Banner(data.TeamBannerCode);
            Team team = Mission.Current.Teams.Add(
                BattleSideEnum.None,
                data.TeamColor,
                data.TeamColor2,
                banner,
                false,
                false,
                true);

            foreach (Team other in teams.Values)
            {
                team.SetIsEnemyOf(other, true);
                other.SetIsEnemyOf(team, true);
            }
            teams.Add(data.TeamId, team);
        }
    }

    private void Spawn(
        TournamentAgentSpawnData data,
        TournamentContestantData contestant,
        ITournamentMissionSession session)
    {
        if (!objectManager.TryGetObject(data.CharacterId, out CharacterObject character)) return;
        if (!teams.TryGetValue(data.TeamId, out Team team)) return;

        TournamentAgentControlRole role = TournamentAgentControlPolicy.Resolve(
            contestant,
            data.ControllerId,
            controllerIdProvider.ControllerId);
        Equipment equipment = BuildEquipment(data.Equipment);
        ApplyMountEquipment(equipment, data.MountEquipment);
        bool hasMount = data.MountAgentId != Guid.Empty;
        if (hasMount && equipment[EquipmentIndex.Horse].IsEmpty) return;
        var origin = new SimpleAgentOrigin(
            character,
            -1,
            null,
            new UniqueTroopDescriptor(data.DescriptorSeed));
        var buildData = new AgentBuildData(origin);
        buildData.InitialPosition(data.Position);
        buildData.InitialDirection(data.Direction);
        buildData.Team(team);
        buildData.Equipment(equipment);
        buildData.Controller(role switch
        {
            TournamentAgentControlRole.HumanPlayer => AgentControllerType.Player,
            TournamentAgentControlRole.NpcAuthority => AgentControllerType.AI,
            _ => AgentControllerType.None
        });
        buildData.ClothingColor1(data.TeamColor);
        buildData.Banner(team.Banner);
        if (hasMount)
        {
            string mountKey = MountCreationKey.GetRandomMountKeyString(
                equipment[EquipmentIndex.Horse].Item,
                data.MountDescriptorSeed);
            buildData.MountKey(mountKey);
        }

        Agent agent = Mission.Current.SpawnAgent(buildData);
        if (!ValidateMount(agent, data))
        {
            FadeInvalidSpawn(agent);
            return;
        }
        agent.Health = data.Health;
        agent.SetWatchState(Agent.WatchState.Alarmed);
        agent.WieldInitialWeapons(
            Agent.WeaponWieldActionType.InstantAfterPickUp,
            Equipment.InitialWeaponEquipPreference.Any);
        agent.FadeIn();
        coopMissionComponent.AgentRegistry.TryRegisterAgent(data.ControllerId, data.AgentId, agent);

        if (role == TournamentAgentControlRole.HumanPlayer)
        {
            Mission.Current.PlayerTeam = team;
            Mission.Current.MainAgent = agent;
        }
        else if (role == TournamentAgentControlRole.NpcAuthority)
        {
            AgentAiWaker.Wake(agent);
        }

        if (hasMount)
        {
            agent.MountAgent.Controller = role == TournamentAgentControlRole.Puppet
                ? AgentControllerType.None
                : AgentControllerType.AI;
            agent.MountAgent.Health = data.MountHealth;
            coopMissionComponent.AgentRegistry.TryRegisterAgent(
                data.ControllerId,
                data.MountAgentId,
                agent.MountAgent);
        }
    }

    private void ApplyMountEquipment(
        Equipment riderEquipment,
        EquipmentElement[] mountElements)
    {
        Equipment mountEquipment = BuildEquipment(mountElements);
        foreach (EquipmentIndex index in new[] { EquipmentIndex.Horse, EquipmentIndex.HorseHarness })
            if (!mountEquipment[index].IsEmpty)
                riderEquipment[index] = mountEquipment[index];
    }

    private bool ValidateMount(Agent agent, TournamentAgentSpawnData data)
    {
        if (data.MountAgentId == Guid.Empty) return agent.MountAgent == null;
        if (agent.MountAgent == null) return false;
        BasicCharacterObject mountCharacter = agent.MountAgent.Character;
        if (!string.IsNullOrEmpty(data.MountCharacterId))
        {
            if (!objectManager.TryGetObject(data.MountCharacterId, out mountCharacter)) return false;
            if (agent.MountAgent.Character != mountCharacter) return false;
        }

        // Native mount agents can legitimately expose no character and the manifest permits that case.
        // Keep the origin created by SpawnAgent instead of constructing SimpleAgentOrigin with a null troop.
        if (mountCharacter == null) return true;

        agent.MountAgent.Origin = new SimpleAgentOrigin(
            mountCharacter,
            -1,
            null,
            new UniqueTroopDescriptor(data.MountDescriptorSeed));
        return true;
    }

    private static void FadeInvalidSpawn(Agent agent)
    {
        if (agent?.MountAgent?.IsActive() == true)
            agent.MountAgent.FadeOut(false, true);
        if (agent?.IsActive() == true)
            agent.FadeOut(false, true);
    }

    private static Equipment BuildEquipment(EquipmentElement[] elements)
    {
        var equipment = new Equipment();
        if (elements == null) return equipment;

        int count = Math.Min(elements.Length, (int)EquipmentIndex.NumEquipmentSetSlots);
        for (int i = 0; i < count; i++)
        {
            if (!elements[i].IsEmpty)
                equipment[(EquipmentIndex)i] = elements[i];
        }
        return equipment;
    }
}
