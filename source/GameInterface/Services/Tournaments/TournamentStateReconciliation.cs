using GameInterface.Services.Tournaments.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Tournaments;

public static class TournamentStateReconciliation
{
    public static IReadOnlyDictionary<string, int> ReconcileContestantScores(
        TournamentSessionSnapshot snapshot,
        IReadOnlyDictionary<string, int> candidateScores,
        out bool changed)
    {
        var result = new Dictionary<string, int>();
        changed = candidateScores == null || candidateScores.Count != (snapshot?.Contestants?.Length ?? 0);
        foreach (TournamentContestantData contestant in snapshot?.Contestants ?? new TournamentContestantData[0])
        {
            if (candidateScores != null &&
                candidateScores.TryGetValue(contestant.SlotId, out int score) &&
                score >= 0)
            {
                result.Add(contestant.SlotId, score);
            }
            else
            {
                result.Add(contestant.SlotId, Math.Max(0, contestant.Score));
                changed = true;
            }
        }
        return result;
    }

    public static TournamentSessionSnapshot[] GetStaleSessions(
        IEnumerable<TournamentSessionSnapshot> localSessions,
        IEnumerable<TournamentSessionSnapshot> authoritativeSessions)
    {
        var authoritativeIds = new HashSet<string>(
            (authoritativeSessions ?? Enumerable.Empty<TournamentSessionSnapshot>())
            .Where(session => session != null && !string.IsNullOrEmpty(session.SessionId))
            .Select(session => session.SessionId));
        return (localSessions ?? Enumerable.Empty<TournamentSessionSnapshot>())
            .Where(session => session != null && !authoritativeIds.Contains(session.SessionId))
            .ToArray();
    }
}
