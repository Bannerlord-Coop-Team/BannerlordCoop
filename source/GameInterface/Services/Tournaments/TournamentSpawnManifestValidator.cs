using GameInterface.Services.Tournaments.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Tournaments;

public static class TournamentSpawnManifestValidator
{
    public static bool IsValid(
        TournamentSpawnManifestData manifest,
        TournamentSessionSnapshot snapshot)
    {
        if (manifest?.Agents == null || snapshot?.Contestants == null || snapshot.Rounds == null) return false;

        TournamentMatchData match = snapshot.Rounds
            .Where(round => round?.Matches != null)
            .SelectMany(round => round.Matches)
            .FirstOrDefault(candidate => candidate?.MatchId == snapshot.CurrentMatchId);
        if (match?.Teams == null || match.Teams.Any(team => team?.ParticipantSlotIds == null)) return false;
        if (!HasExpectedSlots(manifest, match)) return false;

        var contestants = snapshot.Contestants
            .Where(contestant => contestant != null && !string.IsNullOrEmpty(contestant.SlotId))
            .ToDictionary(contestant => contestant.SlotId);
        var agentIds = new HashSet<Guid>();
        return manifest.Agents.All(agent =>
            IsValidAgent(agent, snapshot, match, contestants, agentIds));
    }

    private static bool HasExpectedSlots(
        TournamentSpawnManifestData manifest,
        TournamentMatchData match)
    {
        string[] expectedSlots = match.Teams.SelectMany(team => team.ParticipantSlotIds).ToArray();
        if (manifest.Agents.Length != expectedSlots.Length || manifest.Agents.Any(agent => agent == null))
            return false;
        if (manifest.Agents.Select(agent => agent.SlotId).Distinct().Count() != expectedSlots.Length)
            return false;
        return manifest.Agents.Select(agent => agent.SlotId).OrderBy(id => id)
            .SequenceEqual(expectedSlots.OrderBy(id => id));
    }

    private static bool IsValidAgent(
        TournamentAgentSpawnData agent,
        TournamentSessionSnapshot snapshot,
        TournamentMatchData match,
        IReadOnlyDictionary<string, TournamentContestantData> contestants,
        HashSet<Guid> agentIds)
    {
        if (agent.AgentId == Guid.Empty || !agentIds.Add(agent.AgentId)) return false;
        if (!contestants.TryGetValue(agent.SlotId, out var contestant)) return false;
        if (!HasValidIdentityAndTeam(agent, contestant, snapshot, match)) return false;
        if (!HasValidTransformAndEquipment(agent)) return false;
        return HasValidMount(agent, agentIds);
    }

    private static bool HasValidIdentityAndTeam(
        TournamentAgentSpawnData agent,
        TournamentContestantData contestant,
        TournamentSessionSnapshot snapshot,
        TournamentMatchData match)
    {
        if (agent.CharacterId != contestant.CharacterId ||
            agent.DescriptorSeed != contestant.DescriptorSeed)
            return false;

        TournamentTeamData team = match.Teams.FirstOrDefault(candidate =>
            candidate.ParticipantSlotIds.Contains(agent.SlotId));
        if (team == null ||
            agent.TeamId != team.TeamId ||
            agent.TeamColor != team.TeamColor ||
            agent.TeamColor2 != team.TeamColor2 ||
            agent.TeamBannerCode != team.BannerCode)
            return false;

        string expectedOwner = contestant.IsHuman && !contestant.IsReplaced
            ? contestant.ControllerId
            : snapshot.HostControllerId;
        return !string.IsNullOrEmpty(expectedOwner) && agent.ControllerId == expectedOwner;
    }

    private static bool HasValidTransformAndEquipment(TournamentAgentSpawnData agent)
    {
        return IsFinite(agent.Position) &&
            IsFinite(agent.Direction) &&
            agent.Direction.LengthSquared >= 0.01f &&
            IsFinite(agent.Health) &&
            agent.Health > 0 &&
            ValidEquipment(agent.Equipment);
    }

    private static bool HasValidMount(
        TournamentAgentSpawnData agent,
        HashSet<Guid> agentIds)
    {
        if (agent.MountAgentId == Guid.Empty)
            return string.IsNullOrEmpty(agent.MountCharacterId);
        return agentIds.Add(agent.MountAgentId) &&
            IsFinite(agent.MountHealth) &&
            agent.MountHealth > 0 &&
            ValidEquipment(agent.MountEquipment);
    }
    private static bool ValidEquipment(EquipmentElement[] equipment)
        => equipment != null &&
           equipment.Length <= (int)EquipmentIndex.NumEquipmentSetSlots;

    private static bool IsFinite(Vec3 value)
        => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static bool IsFinite(Vec2 value)
        => IsFinite(value.X) && IsFinite(value.Y);

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}
