using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using SandBox.Tournaments.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using Serilog;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments;

public interface ITournamentSpawnManifestBuilder
{
    TournamentSpawnManifestData Build(
        TournamentSessionSnapshot snapshot,
        CoopTournamentBehavior behavior,
        TournamentFightMissionController fightController,
        long sequence,
        ITournamentMissionSession session);
}

public class TournamentSpawnManifestBuilder : ITournamentSpawnManifestBuilder
{
    private static readonly ILogger Logger = LogManager.GetLogger<TournamentSpawnManifestBuilder>();
    private readonly IObjectManager objectManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private string rejectedManifestMatchId;
    private string agentCountMismatchMatchId;

    public TournamentSpawnManifestBuilder(
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent)
    {
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
    }

    public TournamentSpawnManifestData Build(
        TournamentSessionSnapshot snapshot,
        CoopTournamentBehavior behavior,
        TournamentFightMissionController fightController,
        long sequence,
        ITournamentMissionSession session)
    {
        TournamentMatchData match = snapshot.Rounds
            .SelectMany(round => round.Matches)
            .Single(data => data.MatchId == snapshot.CurrentMatchId);
        List<Agent> agents = fightController._currentTournamentAgents
            .Where(agent => agent != null && !agent.IsMount)
            .ToList();
        int expectedAgentCount = match.Teams.Sum(team => team.ParticipantSlotIds.Length);
        if (agents.Count != expectedAgentCount)
        {
            if (agentCountMismatchMatchId != snapshot.CurrentMatchId)
            {
                Logger.Warning(
                    "[Tournament] Native fight controller has wrong agent count for {MatchId}: agents={AgentCount}, expected={ExpectedCount}",
                    snapshot.CurrentMatchId,
                    agents.Count,
                    expectedAgentCount);
            }
            agentCountMismatchMatchId = snapshot.CurrentMatchId;
            return null;
        }
        agentCountMismatchMatchId = null;

        TournamentTeam[] nativeTeams = behavior.CurrentMatch.Teams.ToArray();
        var assignedParticipants = new HashSet<TournamentParticipant>();
        var assignedSlots = new HashSet<string>();
        var records = new TournamentAgentSpawnData[agents.Count];
        for (int i = 0; i < agents.Count; i++)
        {
            Agent agent = agents[i];
            if (agent.Origin == null || agent.Character is not CharacterObject character ||
                !objectManager.TryGetId(character, out string characterId)) return null;

            TournamentParticipant participant = ResolveParticipant(
                agent,
                character,
                behavior,
                match,
                nativeTeams,
                assignedParticipants);
            if (participant == null)
            {
                Logger.Warning("[Tournament] Could not map spawned agent {CharacterId} seed {Seed} into match {MatchId}",
                    characterId, agent.Origin.UniqueSeed, match.MatchId);
                return null;
            }
            assignedParticipants.Add(participant);
            int nativeTeamIndex = Array.IndexOf(nativeTeams, participant.Team);
            int participantIndex = Array.IndexOf(behavior._participants, participant);
            if (nativeTeamIndex < 0 || nativeTeamIndex >= match.Teams.Length ||
                participantIndex < 0 || participantIndex >= snapshot.Contestants.Length) return null;
            TournamentContestantData contestant = snapshot.Contestants[participantIndex];
            TournamentTeamData team = match.Teams[nativeTeamIndex];
            if (!team.ParticipantSlotIds.Contains(contestant.SlotId)) return null;
            if (!assignedSlots.Add(contestant.SlotId)) return null;
            if (agent.Team == null || agent.Team.Color != team.TeamColor) return null;

            records[i] = BuildAgent(agent, contestant, team, session);
            if (records[i] == null) return null;
        }

        var manifest = new TournamentSpawnManifestData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            sequence,
            records);
        if (!TournamentSpawnManifestValidator.IsValid(manifest, snapshot))
        {
            if (rejectedManifestMatchId != snapshot.CurrentMatchId)
                Logger.Warning(
                    "[Tournament] Locally rejected spawn manifest for {MatchId}: agents={AgentCount}, expected={ExpectedCount}",
                    snapshot.CurrentMatchId,
                    records.Length,
                    expectedAgentCount);
            rejectedManifestMatchId = snapshot.CurrentMatchId;
            return null;
        }
        rejectedManifestMatchId = null;
        Logger.Information(
            "[Tournament] Built spawn manifest for {MatchId}: agents={AgentCount}, sequence={Sequence}",
            snapshot.CurrentMatchId,
            records.Length,
            sequence);

        for (int i = 0; i < agents.Count; i++)
            RegisterAgent(agents[i], records[i], session);
        return manifest;
    }

    private static TournamentParticipant ResolveParticipant(
        Agent agent,
        CharacterObject character,
        CoopTournamentBehavior behavior,
        TournamentMatchData match,
        TournamentTeam[] nativeTeams,
        HashSet<TournamentParticipant> assignedParticipants)
    {
        TournamentParticipant exact = behavior._participants.FirstOrDefault(candidate =>
            !assignedParticipants.Contains(candidate) &&
            candidate.Descriptor.UniqueSeed == agent.Origin.UniqueSeed &&
            candidate.Character == character);
        if (exact != null) return exact;

        for (int teamIndex = 0; teamIndex < nativeTeams.Length && teamIndex < match.Teams.Length; teamIndex++)
        {
            if (agent.Team == null || agent.Team.Color != match.Teams[teamIndex].TeamColor) continue;
            TournamentParticipant candidate = nativeTeams[teamIndex].Participants.FirstOrDefault(participant =>
                !assignedParticipants.Contains(participant) && participant.Character == character);
            if (candidate != null) return candidate;
        }
        return null;
    }

    private TournamentAgentSpawnData BuildAgent(
        Agent agent,
        TournamentContestantData contestant,
        TournamentTeamData team,
        ITournamentMissionSession session)
    {
        if (!TrySerializeEquipment(agent.Equipment, agent.SpawnEquipment, out var equipment)) return null;
        if (!TrySerializeEquipment(
                agent.MountAgent?.Equipment,
                agent.MountAgent?.SpawnEquipment,
                out var mountEquipment)) return null;

        string owner = contestant.IsHuman && !contestant.IsReplaced
            ? contestant.ControllerId
            : session.HostControllerId;
        Guid agentId = Guid.NewGuid();
        Guid mountId = agent.MountAgent == null ? Guid.Empty : Guid.NewGuid();


        string mountCharacterId = null;
        if (agent.MountAgent?.Character is BasicCharacterObject mountCharacter)
            objectManager.TryGetId(mountCharacter, out mountCharacterId);

        TaleWorlds.Library.Vec2 direction = NormalizeDirection(agent.LookDirection.AsVec2);
        return new TournamentAgentSpawnData(
            agentId,
            contestant.SlotId,
            contestant.CharacterId,
            contestant.DescriptorSeed,
            team.TeamId,
            team.TeamColor,
            team.TeamColor2,
            team.BannerCode,
            owner,
            equipment,
            agent.Position.X,
            agent.Position.Y,
            agent.Position.Z,
            direction.X,
            direction.Y,
            agent.Health,
            mountId,
            mountCharacterId,
            agent.MountAgent?.Origin?.UniqueSeed ?? 0,
            mountEquipment,
            agent.MountAgent?.Health ?? 0f);
    }

    private static TaleWorlds.Library.Vec2 NormalizeDirection(TaleWorlds.Library.Vec2 direction)
    {
        if (float.IsNaN(direction.X) || float.IsInfinity(direction.X) ||
            float.IsNaN(direction.Y) || float.IsInfinity(direction.Y) ||
            direction.LengthSquared < 0.01f)
            return new TaleWorlds.Library.Vec2(0f, 1f);
        return direction;
    }

    private void RegisterAgent(
        Agent agent,
        TournamentAgentSpawnData data,
        ITournamentMissionSession session)
    {
        if (data.ControllerId != session.OwnControllerId)
            agent.Controller = AgentControllerType.None;

        coopMissionComponent.AgentRegistry.TryRegisterAgent(data.ControllerId, data.AgentId, agent);
        if (data.MountAgentId != Guid.Empty)
            coopMissionComponent.AgentRegistry.TryRegisterAgent(
                data.ControllerId,
                data.MountAgentId,
                agent.MountAgent);
    }
    private bool TrySerializeEquipment(
        MissionEquipment missionEquipment,
        Equipment spawnEquipment,
        out TournamentEquipmentElementData[] serialized)
    {
        if (missionEquipment == null && spawnEquipment == null)
        {
            serialized = Array.Empty<TournamentEquipmentElementData>();
            return true;
        }

        var records = new List<TournamentEquipmentElementData>();
        for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
        {
            EquipmentIndex index = (EquipmentIndex)i;
            if (missionEquipment != null && i < (int)EquipmentIndex.NumAllWeaponSlots)
            {
                MissionWeapon weapon = missionEquipment[index];
                if (!weapon.IsEmpty)
                {
                    if (!TrySerializeMissionWeapon(i, weapon, out var data))
                    {
                        serialized = null;
                        return false;
                    }
                    records.Add(data);
                    continue;
                }
            }

            if (spawnEquipment == null) continue;
            EquipmentElement element = spawnEquipment[index];
            if (element.IsEmpty) continue;
            if (!objectManager.TryGetId(element.Item, out string itemId))
            {
                serialized = null;
                return false;
            }

            string modifierId = null;
            if (element.ItemModifier != null &&
                !objectManager.TryGetId(element.ItemModifier, out modifierId))
            {
                serialized = null;
                return false;
            }
            records.Add(new TournamentEquipmentElementData(i, itemId, modifierId));
        }
        serialized = records.ToArray();
        return true;
    }

    private bool TrySerializeMissionWeapon(
        int slotIndex,
        MissionWeapon weapon,
        out TournamentEquipmentElementData data)
    {
        data = null;
        if (weapon.Item == null || !objectManager.TryGetId(weapon.Item, out string itemId))
            return false;

        string modifierId = null;
        if (weapon.ItemModifier != null &&
            !objectManager.TryGetId(weapon.ItemModifier, out modifierId))
        {
            return false;
        }
        data = new TournamentEquipmentElementData(
            slotIndex,
            itemId,
            modifierId,
            weapon.Banner?.Serialize(),
            weapon.RawDataForNetwork);
        return true;
    }
}
