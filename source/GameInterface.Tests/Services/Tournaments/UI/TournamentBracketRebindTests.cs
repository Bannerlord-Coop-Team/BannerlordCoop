using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.UI;
using SandBox.ViewModelCollection.Tournament;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments.UI;

public class TournamentBracketRebindTests
{
    public TournamentBracketRebindTests()
    {
        BannerManager.Initialize();
    }

    [Fact]
    public void Shuffle_CanMoveEveryOriginalPosition()
    {
        var values = new List<int> { 0, 1, 2, 3 };
        var upperBounds = new List<int>();

        TournamentGameInterface.Shuffle(values, upperBound =>
        {
            upperBounds.Add(upperBound);
            return 0;
        });

        Assert.Equal(new[] { 1, 2, 3, 0 }, values);
        Assert.Equal(new[] { 4, 3, 2 }, upperBounds);
    }

    [Fact]
    public void MarkParticipantDead_UpdatesCanonicalLiveRowByDescriptorSeed()
    {
        TournamentMatch match = CreateMatch(5, TournamentMatch.MatchState.Started, false);
        TournamentParticipant participant = match.Teams.Single().Participants.Single();
        var participantViewModel = new TournamentParticipantVM();
        typeof(TournamentParticipantVM)
            .GetField("<Participant>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(participantViewModel, participant);

        bool updated = CoopTournamentVM.MarkParticipantDead(
            new[] { participantViewModel },
            participant.Descriptor.UniqueSeed);

        Assert.True(updated);
        Assert.True(participantViewModel.IsDead);
    }

    [Fact]
    public void RebindCanonicalBracket_ReplacesRoundsScoresWinnersAndCurrentMatch()
    {
        var previousRounds = CreateFourRounds(CreateMatch(1, TournamentMatch.MatchState.Ready, false));
        var roundViewModels = CreateRoundViewModels(previousRounds);
        TournamentMatch finishedMatch = CreateMatch(7, TournamentMatch.MatchState.Finished, true);
        TournamentMatch currentMatch = CreateMatch(3, TournamentMatch.MatchState.Ready, false);
        TournamentRound[] canonicalRounds = CreateFourRounds(finishedMatch, currentMatch);

        TournamentMatchVM currentMatchViewModel = CoopTournamentVM.RebindCanonicalBracket(
            roundViewModels,
            canonicalRounds,
            currentMatch,
            index => new TextObject($"Round {index}"));

        Assert.Same(canonicalRounds[0], roundViewModels[0].Round);
        Assert.Same(finishedMatch, roundViewModels[0].Match1.Match);
        Assert.Equal(7, roundViewModels[0].Match1.Match.Teams.Single().Score);
        Assert.Same(
            finishedMatch.Winners.Single(),
            roundViewModels[0].Match1.Match.Winners.Single());
        Assert.Equal(2, roundViewModels[0].Match1.State);
        Assert.Same(roundViewModels[0].Match2, currentMatchViewModel);
        Assert.Same(canonicalRounds[0].CurrentMatch, currentMatchViewModel.Match);
        Assert.Equal(1, currentMatchViewModel.State);
    }

    [Fact]
    public void ApplyCanonicalMatchStates_RestoresLiveStateAfterNativeRefresh()
    {
        TournamentMatch liveMatch = CreateMatch(4, TournamentMatch.MatchState.Started, false);
        TournamentRound[] canonicalRounds = CreateFourRounds(liveMatch);
        var roundViewModels = CreateRoundViewModels(canonicalRounds);
        roundViewModels[0].Match1.State = 1;

        CoopTournamentVM.ApplyCanonicalMatchStates(roundViewModels, liveMatch);

        Assert.Equal(3, roundViewModels[0].Match1.State);
    }

    [Fact]
    public void RebindCanonicalBracket_ClearsPreviouslyInitializedParticipantSlots()
    {
        TournamentMatch previousMatch = CreateMatch(1, TournamentMatch.MatchState.Ready, false);
        var roundViewModels = CreateRoundViewModels(CreateFourRounds(previousMatch));
        TournamentTeamVM previousTeam = roundViewModels[0].Match1.Team1;
        previousTeam.Participant2.IsInitialized = true;
        previousTeam.Participant2.IsValid = true;
        previousTeam.Participant3.IsInitialized = true;
        previousTeam.Participant3.IsValid = true;
        previousTeam.Participant4.IsInitialized = true;
        previousTeam.Participant4.IsValid = true;

        TournamentMatch compactMatch = CreateMatch(2, TournamentMatch.MatchState.Ready, false);
        TournamentRound[] canonicalRounds = CreateFourRounds(compactMatch);

        CoopTournamentVM.RebindCanonicalBracket(
            roundViewModels,
            canonicalRounds,
            compactMatch,
            index => new TextObject($"Round {index}"));

        Assert.False(previousTeam.Participant2.IsInitialized);
        Assert.False(previousTeam.Participant3.IsInitialized);
        Assert.False(previousTeam.Participant4.IsInitialized);
    }

    [Fact]
    public void RebindCanonicalBracket_RepeatedRefreshDoesNotGrowVisualSlotCounts()
    {
        TournamentMatch match = CreateMatch(1, TournamentMatch.MatchState.Ready, false);
        TournamentRound[] canonicalRounds = CreateFourRounds(match);
        var roundViewModels = CreateRoundViewModels(canonicalRounds);

        int roundCount = roundViewModels[0].Count;
        int matchCount = roundViewModels[0].Match1.Count;
        int teamCount = roundViewModels[0].Match1.Team1.Count;

        CoopTournamentVM.RebindCanonicalBracket(
            roundViewModels,
            canonicalRounds,
            match,
            index => new TextObject($"Round {index}"));

        Assert.Equal(roundCount, roundViewModels[0].Count);
        Assert.Equal(matchCount, roundViewModels[0].Match1.Count);
        Assert.Equal(teamCount, roundViewModels[0].Match1.Team1.Count);
    }

    [Fact]
    public void TryApplyScores_AllowsAuthoritativeLaterRoundScoreToReplaceHydratedScore()
    {
        TournamentMatch match = CreateMatch(5, TournamentMatch.MatchState.Started, false);
        var matchData = new TournamentMatchData(
            "match",
            "round",
            (int)TournamentMatch.MatchState.Started,
            1,
            1,
            new[]
            {
                new TournamentTeamData("team", new[] { "slot" }, 5, false, 0, 0, null)
            },
            new string[0]);

        bool applied = TournamentGameInterface.TryApplyScores(
            match,
            matchData,
            new[] { new TournamentTeamScoreData("team", 2) });

        Assert.True(applied);
        Assert.Equal(2, match.Teams.Single().Score);
    }

    [Fact]
    public void AddWinnersToNextRound_FillsAllEightSecondRoundSlots()
    {
        var nextRound = new TournamentRound(
            8,
            1,
            4,
            4,
            TournamentGame.QualificationMode.TeamScore);
        TournamentParticipant[] winners = Enumerable.Range(1, 8)
            .Select(seed => new TournamentParticipant(null, new UniqueTroopDescriptor(seed))
            {
                IsAssigned = true
            })
            .ToArray();

        TournamentGameInterface.AddWinnersToNextRound(nextRound, winners);

        Assert.Single(nextRound.Matches);
        Assert.Equal(
            new[] { 2, 2, 2, 2 },
            nextRound.Matches[0].Teams.Select(team => team.Participants.Count()).ToArray());
        Assert.All(winners, winner => Assert.True(winner.IsAssigned));
    }

    [Fact]
    public void RefreshCanonicalMatch_AllowsEmptyNativeTeamSlots()
    {
        TournamentMatch match = CreateMatch(4, TournamentMatch.MatchState.Ready, false);
        var matchViewModel = new TournamentMatchVM();
        matchViewModel.Initialize(match);
        TournamentTeam team = match.Teams.Single();
        team._participants.Clear();
        team.TeamSize = 2;
        matchViewModel.Team1.Initialize(team);

        var exception = Record.Exception(() => CoopTournamentVM.RefreshCanonicalMatch(matchViewModel));

        Assert.Null(exception);
    }

    private static TournamentRoundVM[] CreateRoundViewModels(TournamentRound[] rounds)
    {
        var result = new[]
        {
            new TournamentRoundVM(),
            new TournamentRoundVM(),
            new TournamentRoundVM(),
            new TournamentRoundVM()
        };

        for (int i = 0; i < result.Length; i++)
            result[i].Initialize(rounds[i], new TextObject($"Round {i}"));
        return result;
    }

    private static TournamentRound[] CreateFourRounds(params TournamentMatch[] firstRoundMatches)
    {
        var rounds = new TournamentRound[4];
        rounds[0] = CreateRound(firstRoundMatches);
        for (int i = 1; i < rounds.Length; i++)
            rounds[i] = CreateRound(CreateMatch(0, TournamentMatch.MatchState.Ready, false));
        return rounds;
    }

    private static TournamentRound CreateRound(params TournamentMatch[] matches)
    {
        var round = new TournamentRound(
            matches.Length,
            matches.Length,
            1,
            1,
            TournamentGame.QualificationMode.TeamScore);
        round.Matches = matches;
        round.CurrentMatchIndex = matches.Length - 1;
        return round;
    }

    private static TournamentMatch CreateMatch(
        int score,
        TournamentMatch.MatchState state,
        bool isWinner)
    {
        var participant = new TournamentParticipant(
            null,
            new UniqueTroopDescriptor(score + 1));
        participant.Score = score;

        var match = new TournamentMatch(
            1,
            1,
            1,
            TournamentGame.QualificationMode.TeamScore);
        match.AddParticipant(participant, true);
        match.Teams.Single().TeamSize = 0;
        match._winners = isWinner
            ? new List<TournamentParticipant> { participant }
            : new List<TournamentParticipant>();
        match.State = state;
        return match;
    }

}
