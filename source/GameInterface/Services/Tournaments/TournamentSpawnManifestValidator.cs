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
        => IsValid(manifest, snapshot, out _);

    public static bool IsValid(
        TournamentSpawnManifestData manifest,
        TournamentSessionSnapshot snapshot,
        out string rejectionReason)
    {
        rejectionReason = null;
        if (manifest?.Agents == null || snapshot?.Contestants == null || snapshot.Rounds == null)
            return Reject("missing manifest agents or snapshot bracket", out rejectionReason);

        TournamentMatchData match = snapshot.Rounds
            .Where(round => round?.Matches != null)
            .SelectMany(round => round.Matches)
            .FirstOrDefault(candidate => candidate?.MatchId == snapshot.CurrentMatchId);
        if (match?.Teams == null || match.Teams.Any(team => team?.ParticipantSlotIds == null))
            return Reject("missing current match teams or slots", out rejectionReason);
        if (!HasExpectedSlots(manifest, match))
            return Reject("manifest slots differ from current match", out rejectionReason);

        var contestants = snapshot.Contestants
            .Where(contestant => contestant != null && !string.IsNullOrEmpty(contestant.SlotId))
            .ToDictionary(contestant => contestant.SlotId);
        var agentIds = new HashSet<Guid>();
        foreach (TournamentAgentSpawnData agent in manifest.Agents)
        {
            if (!IsValidAgent(agent, snapshot, match, contestants, agentIds, out string agentReason))
                return Reject($"slot={agent.SlotId}: {agentReason}", out rejectionReason);
        }
        return true;
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
        HashSet<Guid> agentIds,
        out string rejectionReason)
    {
        rejectionReason = null;
        if (agent.AgentId == Guid.Empty || !agentIds.Add(agent.AgentId))
            return Reject($"empty or duplicate agent id={agent.AgentId}", out rejectionReason);
        if (!contestants.TryGetValue(agent.SlotId, out var contestant))
            return Reject("contestant slot not found", out rejectionReason);
        if (!HasValidIdentityAndTeam(agent, contestant, snapshot, match, out rejectionReason)) return false;
        if (!HasValidTransformAndEquipment(agent, out rejectionReason)) return false;
        return HasValidMount(agent, agentIds, out rejectionReason);
    }

    private static bool HasValidIdentityAndTeam(
        TournamentAgentSpawnData agent,
        TournamentContestantData contestant,
        TournamentSessionSnapshot snapshot,
        TournamentMatchData match,
        out string rejectionReason)
    {
        rejectionReason = null;
        if (agent.CharacterId != contestant.CharacterId ||
            agent.DescriptorSeed != contestant.DescriptorSeed)
            return Reject($"character or seed mismatch: character={agent.CharacterId}, expectedCharacter={contestant.CharacterId}, " +
                $"seed={agent.DescriptorSeed}, expectedSeed={contestant.DescriptorSeed}", out rejectionReason);

        TournamentTeamData team = match.Teams.FirstOrDefault(candidate =>
            candidate.ParticipantSlotIds.Contains(agent.SlotId));
        if (team == null ||
            agent.TeamId != team.TeamId ||
            agent.TeamColor != team.TeamColor ||
            agent.TeamColor2 != team.TeamColor2 ||
            agent.TeamBannerCode != team.BannerCode)
            return Reject($"team mismatch: team={agent.TeamId}, expectedTeam={team?.TeamId}, " +
                $"color={agent.TeamColor}, expectedColor={team?.TeamColor}, color2={agent.TeamColor2}, expectedColor2={team?.TeamColor2}, " +
                $"banner={agent.TeamBannerCode}, expectedBanner={team?.BannerCode}", out rejectionReason);

        string expectedOwner = contestant.IsHuman && !contestant.IsReplaced
            ? contestant.ControllerId
            : snapshot.HostControllerId;
        if (string.IsNullOrEmpty(expectedOwner) || agent.ControllerId != expectedOwner)
            return Reject($"owner mismatch: owner={agent.ControllerId}, expectedOwner={expectedOwner}", out rejectionReason);
        return true;
    }

    private static bool HasValidTransformAndEquipment(TournamentAgentSpawnData agent, out string rejectionReason)
    {
        rejectionReason = null;
        if (!IsFinite(agent.Position))
            return Reject($"non-finite position: x={agent.Position.x}, y={agent.Position.y}, z={agent.Position.z}", out rejectionReason);
        if (!IsFinite(agent.Direction) || agent.Direction.LengthSquared < 0.01f)
            return Reject($"invalid direction: x={agent.Direction.X}, y={agent.Direction.Y}", out rejectionReason);
        if (!IsFinite(agent.Health) || agent.Health <= 0)
            return Reject($"invalid health={agent.Health}", out rejectionReason);
        if (!ValidEquipment(agent.Equipment))
            return Reject($"invalid equipment length={agent.Equipment?.Length}", out rejectionReason);
        return true;
    }

    private static bool HasValidMount(
        TournamentAgentSpawnData agent,
        HashSet<Guid> agentIds,
        out string rejectionReason)
    {
        rejectionReason = null;
        if (agent.MountAgentId == Guid.Empty)
        {
            if (!string.IsNullOrEmpty(agent.MountCharacterId))
                return Reject($"mount character without mount id: character={agent.MountCharacterId}", out rejectionReason);
            return true;
        }
        if (!agentIds.Add(agent.MountAgentId))
            return Reject($"duplicate mount id={agent.MountAgentId}", out rejectionReason);
        if (!IsFinite(agent.MountHealth) || agent.MountHealth <= 0)
            return Reject($"invalid mount health={agent.MountHealth}", out rejectionReason);
        if (!ValidEquipment(agent.MountEquipment))
            return Reject($"invalid mount equipment length={agent.MountEquipment?.Length}", out rejectionReason);
        return true;
    }

    private static bool Reject(string reason, out string rejectionReason)
    {
        rejectionReason = reason;
        return false;
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
