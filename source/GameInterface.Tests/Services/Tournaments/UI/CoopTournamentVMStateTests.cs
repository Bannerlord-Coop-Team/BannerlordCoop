using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using GameInterface.Services.Tournaments.UI;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments.UI;

public class CoopTournamentVMStateTests
{
    [Fact]
    public void CurrentHumanFighter_CanJoinAndBetButCannotWatchOrSkip()
    {
        var snapshot = CreateSnapshot(
            new[] { CreateHumanContestant() },
            Array.Empty<string>(),
            new[] { new TournamentPlayerChoiceData("player-a", TournamentPlayerChoice.Join) },
            false,
            2,
            0,
            3);

        var state = CoopTournamentVM.CalculateUIState(snapshot, "player-a", true);

        Assert.True(state.CanJoin);
        Assert.True(state.CanBet);
        Assert.True(state.CanLeave);
        Assert.False(state.CanWatch);
        Assert.False(state.CanSkip);
        Assert.Equal(2, state.ReadyCount);
        Assert.Equal(0, state.SkipCount);
        Assert.Equal(3, state.VoterCount);
        Assert.Equal(TournamentPlayerChoice.Join, state.SelectedChoice);
    }

    [Fact]
    public void Spectator_CanWatchAndSkipButCannotJoinOrBet()
    {
        var snapshot = CreateSnapshot(
            Array.Empty<TournamentContestantData>(),
            new[] { "player-a" },
            new[] { new TournamentPlayerChoiceData("player-a", TournamentPlayerChoice.Skip) },
            true,
            0,
            1,
            1);

        var state = CoopTournamentVM.CalculateUIState(snapshot, "player-a", true);

        Assert.False(state.CanJoin);
        Assert.False(state.CanBet);
        Assert.True(state.CanWatch);
        Assert.True(state.CanSkip);
        Assert.True(state.CanLeave);
        Assert.Equal(TournamentPlayerChoice.Skip, state.SelectedChoice);
    }


    [Fact]
    public void AcceptedBetSummary_UsesCurrentRoundAmountForRemainingCap()
    {
        var snapshot = CreateSnapshot(
            new[] { CreateHumanContestant() },
            Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            false,
            0,
            0,
            1,
            revision: 5);
        var firstResult = CreateBetResult(
            snapshot,
            revision: 4,
            sequence: 1,
            cumulativeBettedDenars: 100,
            thisRoundBettedDenars: 60,
            expectedPayout: 180);

        Assert.True(CoopTournamentVM.TryGetAcceptedBetSummary(
            snapshot,
            firstResult,
            0,
            out var firstSummary));
        Assert.Equal(100, firstSummary.BettedDenars);
        Assert.Equal(60, firstSummary.ThisRoundBettedDenars);
        Assert.Equal(180, firstSummary.ExpectedPayout);
        Assert.Equal(90, CoopTournamentVM.GetRemainingBetValue(
            150,
            firstSummary.ThisRoundBettedDenars,
            500));

        var cappedResult = CreateBetResult(
            snapshot,
            revision: 6,
            sequence: 2,
            cumulativeBettedDenars: 190,
            thisRoundBettedDenars: 150,
            expectedPayout: 330);
        Assert.True(CoopTournamentVM.TryGetAcceptedBetSummary(
            snapshot,
            cappedResult,
            1,
            out var cappedSummary));
        Assert.Equal(190, cappedSummary.BettedDenars);
        Assert.Equal(0, CoopTournamentVM.GetRemainingBetValue(
            150,
            cappedSummary.ThisRoundBettedDenars,
            500));

        var cappedState = CoopTournamentVM.CalculateUIState(snapshot, "player-a", false);
        Assert.False(cappedState.CanBet);
    }

    [Fact]
    public void BetResults_AreOrderedBySequenceAndCurrentMatchNotSnapshotRevision()
    {
        var snapshot = CreateSnapshot(
            new[] { CreateHumanContestant() },
            Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            false,
            0,
            0,
            1,
            revision: 9);
        var newerSequence = CreateBetResult(snapshot, 8, 3, 50, 50, 90);
        var duplicateSequence = CreateBetResult(snapshot, 10, 3, 100, 100, 180);
        var wrongMatch = new NetworkTournamentBetResult(
            snapshot.SessionId,
            10,
            4,
            "other-match",
            true,
            null,
            100,
            100,
            180,
            false);

        Assert.True(CoopTournamentVM.TryGetAcceptedBetSummary(
            snapshot,
            newerSequence,
            2,
            out _));
        Assert.False(CoopTournamentVM.TryGetAcceptedBetSummary(
            snapshot,
            duplicateSequence,
            3,
            out _));
        Assert.False(CoopTournamentVM.TryGetAcceptedBetSummary(
            snapshot,
            wrongMatch,
            3,
            out _));
    }

    [Fact]
    public void Settlement_ClearsCumulativeAndCurrentRoundBetPresentation()
    {
        var snapshot = CreateSnapshot(
            new[] { CreateHumanContestant() },
            Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            false,
            0,
            0,
            1,
            revision: 9);
        var settlement = new NetworkTournamentBetResult(
            snapshot.SessionId,
            10,
            4,
            "previous-match",
            true,
            "Tournament bet lost",
            150,
            100,
            270,
            true);

        Assert.True(CoopTournamentVM.TryGetAcceptedBetSummary(
            snapshot,
            settlement,
            3,
            out var summary));
        Assert.True(summary.IsSettlement);
        Assert.Equal(0, summary.BettedDenars);
        Assert.Equal(0, summary.ThisRoundBettedDenars);
        Assert.Equal(0, summary.ExpectedPayout);
    }

    private static NetworkTournamentBetResult CreateBetResult(
        TournamentSessionSnapshot snapshot,
        long revision,
        long sequence,
        int cumulativeBettedDenars,
        int thisRoundBettedDenars,
        int expectedPayout)
        => new(
            snapshot.SessionId,
            revision,
            sequence,
            snapshot.CurrentMatchId,
            true,
            null,
            cumulativeBettedDenars,
            thisRoundBettedDenars,
            expectedPayout,
            false);
    private static TournamentContestantData CreateHumanContestant()
        => new(
            "slot-a",
            "character-a",
            1,
            "player-a",
            "Player A",
            true,
            false,
            true,
            "npc-a");

    private static TournamentSessionSnapshot CreateSnapshot(
        TournamentContestantData[] contestants,
        string[] spectators,
        TournamentPlayerChoiceData[] choices,
        bool skipAllowed,
        int readyCount,
        int skipCount,
        int voterCount,
        long revision = 1,
        TournamentSessionPhase phase = TournamentSessionPhase.AwaitingChoices)
    {
        var match = new TournamentMatchData(
            "match-a",
            "round-a",
            0,
            1,
            1,
            new[]
            {
                new TournamentTeamData(
                    "team-a",
                    new[] { "slot-a" },
                    0,
                    false,
                    0,
                    null)
            },
            Array.Empty<string>());

        return new TournamentSessionSnapshot(
            "session-a",
            "mission-a",
            "town-a",
            "arena-a",
            "prize-a",
            phase,
            revision,
            1,
            match.MatchId,
            "host-a",
            Array.Empty<string>(),
            contestants,
            spectators,
            choices,
            new[] { new TournamentRoundData("round-a", 0, 0, new[] { match }) },
            readyCount,
            skipCount,
            voterCount,
            skipAllowed,
            false,
            null);
    }
}
