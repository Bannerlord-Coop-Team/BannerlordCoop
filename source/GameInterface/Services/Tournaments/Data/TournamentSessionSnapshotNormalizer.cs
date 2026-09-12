using System;
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
            snapshot.SuccessorControllerIds ?? Array.Empty<string>(),
            snapshot.Contestants ?? Array.Empty<TournamentContestantData>(),
            snapshot.SpectatorControllerIds ?? Array.Empty<string>(),
            snapshot.Choices ?? Array.Empty<TournamentPlayerChoiceData>(),
            (snapshot.Rounds ?? Array.Empty<TournamentRoundData>())
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
            (round.Matches ?? Array.Empty<TournamentMatchData>())
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
            (match.Teams ?? Array.Empty<TournamentTeamData>())
                .Where(team => team != null)
                .Select(NormalizeTeam)
                .ToArray(),
            match.WinnerSlotIds ?? Array.Empty<string>(),
            match.QualificationMode);
    }

    private static TournamentTeamData NormalizeTeam(TournamentTeamData team)
    {
        return new TournamentTeamData(
            team.TeamId,
            team.ParticipantSlotIds ?? Array.Empty<string>(),
            team.Score,
            team.IsWinner,
            team.TeamColor,
            team.TeamColor2,
            team.BannerCode);
    }
}
