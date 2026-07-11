using System.Linq;

namespace GameInterface.Services.Tournaments.Data;

public static class TournamentSessionSnapshotNormalizer
{
    public static TournamentSessionSnapshot Normalize(TournamentSessionSnapshot snapshot)
    {
        if (snapshot == null)
            return null;
        if (IsNormalized(snapshot))
            return snapshot;

        return new TournamentSessionSnapshot(
            snapshot.SessionId,
            snapshot.MissionInstanceId,
            snapshot.TownId,
            snapshot.SceneName,
            snapshot.PrizeItemId,
            snapshot.Phase,
            snapshot.Revision,
            snapshot.BracketRevision,
            snapshot.CurrentMatchId,
            snapshot.HostControllerId,
            snapshot.SuccessorControllerIds ?? new string[0],
            snapshot.Contestants ?? new TournamentContestantData[0],
            snapshot.SpectatorControllerIds ?? new string[0],
            snapshot.Choices ?? new TournamentPlayerChoiceData[0],
            (snapshot.Rounds ?? new TournamentRoundData[0])
                .Where(round => round != null)
                .Select(NormalizeRound)
                .ToArray(),
            snapshot.ReadyCount,
            snapshot.SkipCount,
            snapshot.VoterCount,
            snapshot.SkipAllowed,
            snapshot.IsCompleted,
            snapshot.WinnerSlotId);
    }

    private static bool IsNormalized(TournamentSessionSnapshot snapshot)
    {
        return snapshot.SuccessorControllerIds != null &&
               snapshot.Contestants != null &&
               snapshot.SpectatorControllerIds != null &&
               snapshot.Choices != null &&
               snapshot.Rounds != null &&
               snapshot.Rounds.All(round =>
                   round?.Matches != null &&
                   round.Matches.All(match =>
                       match?.Teams != null &&
                       match.WinnerSlotIds != null &&
                       match.Teams.All(team => team?.ParticipantSlotIds != null)));
    }
    private static TournamentRoundData NormalizeRound(TournamentRoundData round)
    {
        return new TournamentRoundData(
            round.RoundId,
            round.RoundIndex,
            round.CurrentMatchIndex,
            (round.Matches ?? new TournamentMatchData[0])
                .Where(match => match != null)
                .Select(NormalizeMatch)
                .ToArray());
    }

    private static TournamentMatchData NormalizeMatch(TournamentMatchData match)
    {
        return new TournamentMatchData(
            match.MatchId,
            match.RoundId,
            match.State,
            match.TeamSize,
            match.NumberOfWinnerParticipants,
            (match.Teams ?? new TournamentTeamData[0])
                .Where(team => team != null)
                .Select(NormalizeTeam)
                .ToArray(),
            match.WinnerSlotIds ?? new string[0],
            match.QualificationMode);
    }

    private static TournamentTeamData NormalizeTeam(TournamentTeamData team)
    {
        return new TournamentTeamData(
            team.TeamId,
            team.ParticipantSlotIds ?? new string[0],
            team.Score,
            team.IsWinner,
            team.TeamColor,
            team.TeamColor2,
            team.BannerCode);
    }
}
