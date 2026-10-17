using GameInterface.Services.Tournaments.Data;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.TournamentGames;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal interface ILadysKnightOutTournamentProgress
{
    LadysKnightOutTournamentOutcome Observe(string questId, string controllerId,
        TournamentSessionSnapshot snapshot, out bool started, out int round);
    void RemoveSession(string sessionId);
    void RemoveQuest(string questId);
    void RecordOutcome(string questId, LadysKnightOutBranch branch);
    bool TryTakeOutcome(string questId, LadysKnightOutBranch branch);
}

internal enum LadysKnightOutTournamentOutcome { Ongoing, Eliminated, Champion }

internal sealed class LadysKnightOutTournamentProgress : ILadysKnightOutTournamentProgress
{
    private sealed class Participation
    {
        internal string SessionId;
        internal string MatchId;
        internal string SlotId;
        internal long Revision;
        internal long BracketRevision;
        internal int Round;
    }

    private readonly Dictionary<string, Participation> participation = new();
    private readonly Dictionary<string, LadysKnightOutBranch> outcomes = new();

    public LadysKnightOutTournamentOutcome Observe(string questId, string controllerId,
        TournamentSessionSnapshot snapshot, out bool started, out int round)
    {
        started = false;
        round = 0;
        if (snapshot == null) return LadysKnightOutTournamentOutcome.Ongoing;
        var contestant = snapshot.Contestants.FirstOrDefault(slot => slot.IsHuman &&
            !slot.IsReplaced && slot.ControllerId == controllerId);
        if (participation.TryGetValue(questId, out var previous) && previous.SessionId == snapshot.SessionId)
        {
            if (snapshot.Revision <= previous.Revision) return LadysKnightOutTournamentOutcome.Ongoing;
            previous.Revision = snapshot.Revision;
            if (contestant == null || contestant.SlotId != previous.SlotId)
                previous.MatchId = null;
            var match = snapshot.Rounds.SelectMany(item => item.Matches)
                .FirstOrDefault(item => item.MatchId == previous.MatchId);
            if (match?.State == (int)TournamentMatch.MatchState.Finished &&
                snapshot.BracketRevision > previous.BracketRevision)
            {
                previous.MatchId = null;
                round = previous.Round;
                if (!match.WinnerSlotIds.Contains(previous.SlotId))
                    return LadysKnightOutTournamentOutcome.Eliminated;
                if (snapshot.IsCompleted && snapshot.WinnerSlotId == previous.SlotId)
                    return LadysKnightOutTournamentOutcome.Champion;
            }
        }

        if (snapshot.Phase != TournamentSessionPhase.LiveMatch || contestant == null ||
            !snapshot.Choices.Any(choice => choice.ControllerId == controllerId && choice.Choice == TournamentPlayerChoice.Join))
            return LadysKnightOutTournamentOutcome.Ongoing;
        var currentRound = snapshot.Rounds.FirstOrDefault(item => item.Matches.Any(match =>
            match.MatchId == snapshot.CurrentMatchId && match.State != (int)TournamentMatch.MatchState.Finished &&
            match.Teams.Any(team => team.ParticipantSlotIds.Contains(contestant.SlotId))));
        if (currentRound == null) return LadysKnightOutTournamentOutcome.Ongoing;
        started = previous == null || previous.SessionId != snapshot.SessionId;
        participation[questId] = new Participation
        {
            SessionId = snapshot.SessionId,
            MatchId = snapshot.CurrentMatchId,
            SlotId = contestant.SlotId,
            Revision = snapshot.Revision,
            BracketRevision = snapshot.BracketRevision,
            Round = currentRound.RoundIndex,
        };
        return LadysKnightOutTournamentOutcome.Ongoing;
    }

    public void RemoveSession(string sessionId)
    {
        foreach (var entry in participation.Where(pair => pair.Value.SessionId == sessionId).ToArray())
            participation.Remove(entry.Key);
    }

    public void RemoveQuest(string questId)
    {
        participation.Remove(questId);
        outcomes.Remove(questId);
    }

    public void RecordOutcome(string questId, LadysKnightOutBranch branch) => outcomes[questId] = branch;

    public bool TryTakeOutcome(string questId, LadysKnightOutBranch branch)
    {
        if (!outcomes.TryGetValue(questId, out var outcome) || outcome != branch) return false;
        outcomes.Remove(questId);
        return true;
    }
}
