using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.UI;
using SandBox.ViewModelCollection.Tournament;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
    public void RebindCanonicalBracket_StableSlotReusesPortraitWithoutBindingNotifications()
    {
        CharacterObject character = CreateCharacter("stable-character");
        TournamentParticipant previousParticipant = CreateParticipant(character, 101, 1);
        var roundViewModels = CreateRoundViewModels(
            CreateFourRounds(CreateMatch(0, TournamentMatch.MatchState.Ready, false)));
        TournamentParticipantVM participantViewModel = roundViewModels[0].Match1.Team1.Participant1;
        SetParticipantIdentity(participantViewModel, previousParticipant);
        participantViewModel.IsDead = true;
        participantViewModel.Name = "stale-name";
        participantViewModel.State = 2;
        participantViewModel.IsMainHero = true;
        var previousCharacter = participantViewModel.Character;
        var previousVisual = participantViewModel.Visual;
        var bindingNotifications = new List<string>();
        participantViewModel.PropertyChangedWithValue += (_, args) =>
            bindingNotifications.Add(args.PropertyName);

        TournamentParticipant canonicalParticipant = CreateParticipant(character, 101, 9);
        TournamentMatch canonicalMatch = CreateMatch(
            new[] { canonicalParticipant },
            TournamentMatch.MatchState.Finished,
            0xFF112233,
            canonicalParticipant);
        TournamentRound[] canonicalRounds = CreateFourRounds(canonicalMatch);

        TournamentMatchVM currentMatchViewModel = CoopTournamentVM.RebindCanonicalBracket(
            roundViewModels,
            canonicalRounds,
            canonicalMatch,
            index => new TextObject($"Round {index}"));

        Assert.Same(previousCharacter, participantViewModel.Character);
        Assert.Same(previousVisual, participantViewModel.Visual);
        Assert.DoesNotContain(nameof(TournamentParticipantVM.Character), bindingNotifications);
        Assert.DoesNotContain(nameof(TournamentParticipantVM.Visual), bindingNotifications);
        Assert.Same(canonicalParticipant, participantViewModel.Participant);
        Assert.Same(canonicalParticipant, GetLatestParticipant(participantViewModel));
        Assert.Equal("9", participantViewModel.Score);
        Assert.True(participantViewModel.IsQualifiedForNextRound);
        Assert.Equal(Color.FromUint(0xFF112233), participantViewModel.TeamColor);
        Assert.False(participantViewModel.IsDead);
        Assert.Equal("stable-character", participantViewModel.Name);
        Assert.Equal(1, participantViewModel.State);
        Assert.False(participantViewModel.IsMainHero);
        Assert.Equal(2, roundViewModels[0].Match1.State);
        Assert.Same(roundViewModels[0].Match1, currentMatchViewModel);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void RebindCanonicalBracket_IdentityChangeRefreshesBothPortraitBindings(
        bool changeCharacter,
        bool changeDescriptor)
    {
        CharacterObject previousCharacterObject = CreateCharacter("previous-character");
        TournamentParticipant previousParticipant = CreateParticipant(previousCharacterObject, 201, 1);
        var roundViewModels = CreateRoundViewModels(
            CreateFourRounds(CreateMatch(0, TournamentMatch.MatchState.Ready, false)));
        TournamentParticipantVM participantViewModel = roundViewModels[0].Match1.Team1.Participant1;
        SetParticipantIdentity(participantViewModel, previousParticipant);
        participantViewModel.IsDead = true;
        var previousCharacter = participantViewModel.Character;
        var previousVisual = participantViewModel.Visual;

        CharacterObject canonicalCharacter = changeCharacter
            ? CreateCharacter("replacement-character")
            : previousCharacterObject;
        int canonicalDescriptor = changeDescriptor ? 202 : 201;
        TournamentParticipant canonicalParticipant = CreateParticipant(
            canonicalCharacter,
            canonicalDescriptor,
            4);
        TournamentMatch canonicalMatch = CreateMatch(
            new[] { canonicalParticipant },
            TournamentMatch.MatchState.Ready,
            0xFF445566);
        TournamentRound[] canonicalRounds = CreateFourRounds(canonicalMatch);
        int portraitRefreshes = 0;

        CoopTournamentVM.RebindCanonicalBracket(
            roundViewModels,
            canonicalRounds,
            canonicalMatch,
            index => new TextObject($"Round {index}"),
            (viewModel, participant, color) =>
            {
                portraitRefreshes++;
                ReplacePortrait(viewModel);
            });

        Assert.Equal(1, portraitRefreshes);
        Assert.NotSame(previousCharacter, participantViewModel.Character);
        Assert.NotSame(previousVisual, participantViewModel.Visual);
        Assert.Same(canonicalParticipant, participantViewModel.Participant);
        Assert.False(participantViewModel.IsDead);
    }

    [Fact]
    public void RebindCanonicalBracket_DoesNotSharePortraitAcrossRoundSlots()
    {
        CharacterObject character = CreateCharacter("advancing-character");
        TournamentParticipant previousParticipant = CreateParticipant(character, 301, 1);
        var roundViewModels = CreateRoundViewModels(
            CreateFourRounds(CreateMatch(0, TournamentMatch.MatchState.Ready, false)));
        TournamentParticipantVM firstRoundViewModel = roundViewModels[0].Match1.Team1.Participant1;
        TournamentParticipantVM secondRoundViewModel = roundViewModels[1].Match1.Team1.Participant1;
        SetParticipantIdentity(firstRoundViewModel, previousParticipant);
        var firstRoundCharacter = firstRoundViewModel.Character;
        var firstRoundVisual = firstRoundViewModel.Visual;

        TournamentParticipant canonicalParticipant = CreateParticipant(character, 301, 5);
        TournamentMatch firstRoundMatch = CreateMatch(
            new[] { canonicalParticipant },
            TournamentMatch.MatchState.Finished,
            0xFF778899,
            canonicalParticipant);
        TournamentMatch secondRoundMatch = CreateMatch(
            new[] { canonicalParticipant },
            TournamentMatch.MatchState.Ready,
            0xFF778899);
        TournamentRound[] canonicalRounds = CreateFourRounds(firstRoundMatch);
        canonicalRounds[1] = CreateRound(secondRoundMatch);
        int portraitRefreshes = 0;

        CoopTournamentVM.RebindCanonicalBracket(
            roundViewModels,
            canonicalRounds,
            secondRoundMatch,
            index => new TextObject($"Round {index}"),
            (viewModel, participant, color) =>
            {
                portraitRefreshes++;
                ReplacePortrait(viewModel);
            });

        Assert.Equal(1, portraitRefreshes);
        Assert.Same(firstRoundCharacter, firstRoundViewModel.Character);
        Assert.Same(firstRoundVisual, firstRoundViewModel.Visual);
        Assert.NotSame(firstRoundCharacter, secondRoundViewModel.Character);
        Assert.NotSame(firstRoundVisual, secondRoundViewModel.Visual);
        Assert.Same(canonicalParticipant, firstRoundViewModel.Participant);
        Assert.Same(canonicalParticipant, secondRoundViewModel.Participant);
    }

    [Fact]
    public void RebindCanonicalBracket_EmptyIntervalClearsIdentityBeforeFreshPortrait()
    {
        CharacterObject character = CreateCharacter("returning-character");
        TournamentParticipant previousParticipant = CreateParticipant(character, 401, 1);
        var roundViewModels = CreateRoundViewModels(
            CreateFourRounds(CreateMatch(0, TournamentMatch.MatchState.Ready, false)));
        TournamentParticipantVM participantViewModel = roundViewModels[0].Match1.Team1.Participant1;
        SetParticipantIdentity(participantViewModel, previousParticipant);
        participantViewModel.IsDead = true;
        var previousCharacter = participantViewModel.Character;
        var previousVisual = participantViewModel.Visual;
        TournamentMatch emptyMatch = CreateMatch(
            Array.Empty<TournamentParticipant>(),
            TournamentMatch.MatchState.Ready,
            0xFF99AABB);
        TournamentRound[] emptyRounds = CreateFourRounds(emptyMatch);

        CoopTournamentVM.RebindCanonicalBracket(
            roundViewModels,
            emptyRounds,
            emptyMatch,
            index => new TextObject($"Round {index}"),
            (viewModel, participant, color) => ReplacePortrait(viewModel));

        Assert.False(participantViewModel.IsValid);
        Assert.True(participantViewModel.IsInitialized);
        Assert.Null(participantViewModel.Participant);
        Assert.Null(GetLatestParticipant(participantViewModel));
        Assert.False(participantViewModel.IsDead);

        TournamentParticipant returningParticipant = CreateParticipant(character, 401, 2);
        TournamentMatch returningMatch = CreateMatch(
            new[] { returningParticipant },
            TournamentMatch.MatchState.Ready,
            0xFF99AABB);
        TournamentRound[] returningRounds = CreateFourRounds(returningMatch);
        int portraitRefreshes = 0;

        CoopTournamentVM.RebindCanonicalBracket(
            roundViewModels,
            returningRounds,
            returningMatch,
            index => new TextObject($"Round {index}"),
            (viewModel, participant, color) =>
            {
                portraitRefreshes++;
                ReplacePortrait(viewModel);
            });

        Assert.Equal(1, portraitRefreshes);
        Assert.NotSame(previousCharacter, participantViewModel.Character);
        Assert.NotSame(previousVisual, participantViewModel.Visual);
        Assert.Same(returningParticipant, participantViewModel.Participant);
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
            System.Array.Empty<string>());

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

    private static CharacterObject CreateCharacter(string stringId)
    {
        var character = new CharacterObject { StringId = stringId };
        character._basicName = new TextObject(stringId);
        return character;
    }

    private static TournamentParticipant CreateParticipant(
        CharacterObject character,
        int descriptorSeed,
        int score)
    {
        var participant = new TournamentParticipant(
            character,
            new UniqueTroopDescriptor(descriptorSeed));
        participant.Score = score;
        return participant;
    }

    private static TournamentMatch CreateMatch(
        IReadOnlyList<TournamentParticipant> participants,
        TournamentMatch.MatchState state,
        uint teamColor,
        params TournamentParticipant[] winners)
    {
        int teamSize = Math.Max(1, participants.Count);
        var match = new TournamentMatch(
            teamSize,
            1,
            1,
            TournamentGame.QualificationMode.TeamScore);
        var team = new TournamentTeam(
            teamSize,
            teamColor,
            Banner.CreateOneColoredEmptyBanner(119));
        foreach (TournamentParticipant participant in participants)
            team._participants.Add(participant);

        match._teams[0] = team;
        match._participants.Clear();
        match._participants.AddRange(participants);
        match._winners = winners.ToList();
        match.State = state;
        return match;
    }

    private static void SetParticipantIdentity(
        TournamentParticipantVM participantViewModel,
        TournamentParticipant participant)
    {
        typeof(TournamentParticipantVM)
            .GetField("<Participant>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(participantViewModel, participant);
        typeof(TournamentParticipantVM)
            .GetField("_latestParticipant", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(participantViewModel, participant);
        participantViewModel.IsInitialized = true;
        participantViewModel.IsValid = true;
        participantViewModel.State = 1;
        participantViewModel.Name = participant.Character.Name.ToString();
        participantViewModel.IsMainHero = false;
    }

    private static TournamentParticipant GetLatestParticipant(
        TournamentParticipantVM participantViewModel)
        => (TournamentParticipant)typeof(TournamentParticipantVM)
            .GetField("_latestParticipant", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(participantViewModel);

    private static void ReplacePortrait(TournamentParticipantVM participantViewModel)
    {
        var replacement = new TournamentParticipantVM();
        participantViewModel.Character = replacement.Character;
        participantViewModel.Visual = replacement.Visual;
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
