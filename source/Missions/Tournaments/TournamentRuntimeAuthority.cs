using GameInterface.Services.Tournaments.Data;
using System;
using System.Linq;

namespace Missions.Tournaments;

public static class TournamentRuntimeAuthority
{
    public static bool ShouldApplyHostAggregate(
        Guid agentId,
        TournamentSpawnManifestData manifest,
        TournamentSessionSnapshot snapshot,
        string localControllerId)
    {
        if (agentId == Guid.Empty || manifest?.Agents == null ||
            snapshot?.Contestants == null || string.IsNullOrEmpty(localControllerId)) return true;

        TournamentAgentSpawnData spawn = manifest.Agents.FirstOrDefault(data =>
            data != null && (data.AgentId == agentId || data.MountAgentId == agentId));
        if (spawn == null) return true;
        TournamentContestantData contestant = snapshot.Contestants.FirstOrDefault(data =>
            data != null && data.SlotId == spawn.SlotId);
        return contestant == null ||
               !contestant.IsHuman ||
               contestant.IsReplaced ||
               contestant.ControllerId != localControllerId;
    }
}
