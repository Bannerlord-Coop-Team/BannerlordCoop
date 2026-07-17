using GameInterface.Services.Tournaments.Data;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Tournaments;

/// <summary>Rebinds server-hosted NPC records to the currently elected host after migration.</summary>
public static class TournamentManifestAuthority
{
    public static bool CanResume(
        TournamentSpawnManifestData manifest,
        TournamentSessionSnapshot snapshot) =>
        manifest != null &&
        snapshot != null &&
        manifest.SessionId == snapshot.SessionId &&
        manifest.MatchId == snapshot.CurrentMatchId &&
        manifest.BracketRevision == snapshot.BracketRevision &&
        manifest.Revision <= snapshot.Revision &&
        manifest.Agents != null &&
        !manifest.Agents.Any(data => data == null);

    public static TournamentSpawnManifestData Normalize(
        TournamentSpawnManifestData manifest,
        TournamentSessionSnapshot snapshot)
    {
        if (manifest == null || snapshot == null) return manifest;
        if (manifest.Agents == null || snapshot.Contestants == null ||
            manifest.Agents.Any(data => data == null) || snapshot.Contestants.Any(data => data == null))
            return null;
        var contestants = snapshot.Contestants.ToDictionary(contestant => contestant.SlotId);
        TournamentAgentSpawnData[] agents = manifest.Agents
            .Select(data => NormalizeAgent(data, contestants, snapshot.HostControllerId))
            .ToArray();
        return new TournamentSpawnManifestData(
            manifest.SessionId,
            manifest.MatchId,
            manifest.Revision,
            manifest.BracketRevision,
            manifest.Sequence,
            agents);
    }

    private static TournamentAgentSpawnData NormalizeAgent(
        TournamentAgentSpawnData data,
        Dictionary<string, TournamentContestantData> contestants,
        string hostControllerId)
    {
        if (data == null || !contestants.TryGetValue(data.SlotId, out var contestant)) return data;
        string owner = contestant.IsHuman && !contestant.IsReplaced
            ? data.ControllerId
            : hostControllerId;
        if (owner == data.ControllerId) return data;
        return CopyWithOwner(data, owner);
    }

    private static TournamentAgentSpawnData CopyWithOwner(TournamentAgentSpawnData data, string owner)
        => new TournamentAgentSpawnData(
            data.AgentId,
            data.SlotId,
            data.CharacterId,
            data.DescriptorSeed,
            data.TeamId,
            data.TeamColor,
            data.TeamColor2,
            data.TeamBannerCode,
            owner,
            data.Equipment,
            data.Position,
            data.Direction,
            data.Health,
            data.MountAgentId,
            data.MountCharacterId,
            data.MountDescriptorSeed,
            data.MountEquipment,
            data.MountHealth);
}
