using GameInterface.Services.Issues.Quests.LadysKnightOut;
using GameInterface.Services.Tournaments.Data;
using System;
using TaleWorlds.CampaignSystem.TournamentGames;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class LadysKnightOutTournamentProgressTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CountsAnOwnersEliminationAtTheAuthoritativeRoundOnce(int round)
    {
        var progress = new LadysKnightOutTournamentProgress();
        Assert.Equal(LadysKnightOutTournamentOutcome.Ongoing,
            progress.Observe("quest", "owner", Snapshot(round: round), out _));
        var result = Snapshot(revision: 2, finished: true, round: round);
        Assert.Equal(LadysKnightOutTournamentOutcome.Eliminated,
            progress.Observe("quest", "owner", result, out var eliminatedRound));
        Assert.Equal(round, eliminatedRound);
        Assert.Equal(LadysKnightOutTournamentOutcome.Ongoing,
            progress.Observe("quest", "owner", result, out _));
        Assert.Equal(LadysKnightOutTournamentOutcome.Ongoing,
            progress.Observe("quest", "owner", Snapshot(revision: 3, finished: true, round: round), out _));
    }

    [Fact]
    public void AQualifyingMatchIsIntermediateProgressUntilTheTournamentFinishes()
    {
        var progress = new LadysKnightOutTournamentProgress();
        progress.Observe("quest", "owner", Snapshot(), out _);
        Assert.Equal(LadysKnightOutTournamentOutcome.Ongoing,
            progress.Observe("quest", "owner", Snapshot(revision: 2, finished: true, winner: true), out _));
        progress.Observe("quest", "owner", Snapshot(revision: 3, round: 3), out _);
        Assert.Equal(LadysKnightOutTournamentOutcome.Champion,
            progress.Observe("quest", "owner", Snapshot(revision: 4, finished: true, winner: true, completed: true, round: 3), out _));
    }

    [Theory]
    [InlineData("other", TournamentPlayerChoice.Join)]
    [InlineData("owner", TournamentPlayerChoice.Watch)]
    public void OtherPlayersAndSpectatorsDoNotAcquireMatchProgress(string controller, TournamentPlayerChoice choice)
    {
        var progress = new LadysKnightOutTournamentProgress();
        progress.Observe("quest", controller, Snapshot(choice: choice), out _);
        Assert.Equal(LadysKnightOutTournamentOutcome.Ongoing,
            progress.Observe("quest", controller, Snapshot(revision: 2, finished: true, winner: true, completed: true, choice: choice), out _));
    }

    [Fact]
    public void ANewQuestCannotClaimAnAlreadyFinishedMatch()
    {
        var progress = new LadysKnightOutTournamentProgress();
        Assert.Equal(LadysKnightOutTournamentOutcome.Ongoing,
            progress.Observe("new-quest", "owner", Snapshot(revision: 2, finished: true, winner: true, completed: true), out _));
    }

    [Fact]
    public void TheNpcReplacingADepartedPlayerCannotFinishTheirQuest()
    {
        var progress = new LadysKnightOutTournamentProgress();
        progress.Observe("quest", "owner", Snapshot(), out _);
        Assert.Equal(LadysKnightOutTournamentOutcome.Ongoing,
            progress.Observe("quest", "owner", Snapshot(revision: 2, finished: true, winner: true, completed: true, replaced: true), out _));
    }

    [Fact]
    public void RemovalDiscardsTheOldSessionAndAllowsAFutureTournament()
    {
        var progress = new LadysKnightOutTournamentProgress();
        progress.Observe("quest", "owner", Snapshot(), out _);
        progress.RemoveSession("session");
        Assert.Equal(LadysKnightOutTournamentOutcome.Ongoing,
            progress.Observe("quest", "owner", Snapshot(revision: 2, finished: true), out _));
        progress.Observe("quest", "owner", Snapshot(session: "next-session"), out _);
        Assert.Equal(LadysKnightOutTournamentOutcome.Eliminated,
            progress.Observe("quest", "owner", Snapshot(session: "next-session", revision: 2, finished: true), out _));
    }

    private static TournamentSessionSnapshot Snapshot(long revision = 1, bool finished = false, bool winner = false,
        bool completed = false, bool replaced = false, int round = 0, TournamentPlayerChoice choice = TournamentPlayerChoice.Join,
        string session = "session")
    {
        var contestant = new TournamentContestantData("slot", replaced ? "npc" : "hero", 1,
            replaced ? null : "owner", "Champion", !replaced, replaced, false, null);
        var opponent = new TournamentContestantData("opponent", "opponent-hero", 2,
            null, "Opponent", false, false, false, null);
        var currentSlot = choice == TournamentPlayerChoice.Watch ? "opponent" : "slot";
        var team = new TournamentTeamData("team", new[] { currentSlot }, 0, winner, 0, null);
        var match = new TournamentMatchData("match-" + round, "round-" + round,
            finished ? (int)TournamentMatch.MatchState.Finished : (int)TournamentMatch.MatchState.Ready,
            1, 1, new[] { team }, winner ? new[] { currentSlot } : new[] { "opponent" });
        return new TournamentSessionSnapshot(session, "mission", "town", "arena", "prize",
            completed ? TournamentSessionPhase.Completed : finished ? TournamentSessionPhase.AwaitingChoices : TournamentSessionPhase.LiveMatch,
            revision, finished ? revision : revision - 1, match.MatchId, "owner", Array.Empty<string>(), new[] { contestant, opponent },
            Array.Empty<string>(), new[] { new TournamentPlayerChoiceData("owner", choice) },
            new[] { new TournamentRoundData("round-" + round, round, 0, new[] { match }) }, 1, 0, 1, false, completed,
            completed && winner ? currentSlot : null);
    }
}
