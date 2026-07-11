using GameInterface.Services.Tournaments.Data;
using System.Linq;

namespace Missions.Tournaments;

public static class TournamentSpawnIdentityResolver
{
    public static bool TryResolve(
        TournamentSessionSnapshot snapshot,
        TournamentMatchData match,
        int descriptorSeed,
        string characterId,
        int nativeTeamIndex,
        out TournamentContestantData contestant,
        out TournamentTeamData team)
    {
        contestant = null;
        team = null;
        if (snapshot == null || match == null || string.IsNullOrEmpty(characterId)) return false;
        if (nativeTeamIndex < 0 || nativeTeamIndex >= match.Teams.Length) return false;

        TournamentContestantData[] candidates = snapshot.Contestants
            .Where(data => data.DescriptorSeed == descriptorSeed && data.CharacterId == characterId)
            .ToArray();
        if (candidates.Length != 1) return false;

        contestant = candidates[0];
        team = match.Teams[nativeTeamIndex];
        return team.ParticipantSlotIds.Contains(contestant.SlotId);
    }
}
