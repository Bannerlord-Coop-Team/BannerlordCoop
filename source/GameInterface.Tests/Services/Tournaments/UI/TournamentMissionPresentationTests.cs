using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.UI;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments.UI;

public class TournamentMissionPresentationTests
{
    [Fact]
    public void AwaitingChoices_ShowsBracketCameraAndCapturesInput()
    {
        var state = TournamentMissionPresentationState.From(
            CreateSnapshot(TournamentSessionPhase.AwaitingChoices));

        Assert.True(state.ShouldShowUI);
    }

    [Fact]
    public void CanonicalLiveMatch_HidesBracketClearsCustomCameraAndReleasesInput()
    {
        var state = TournamentMissionPresentationState.From(
            CreateSnapshot(TournamentSessionPhase.LiveMatch));

        Assert.False(state.ShouldShowUI);
    }

    [Fact]
    public void CanonicalLiveMatchTransition_DisablesPresentationOnlyOnce()
    {
        var presentation = new TournamentMatchPresentation(false);

        Assert.True(presentation.Observe(true));
        Assert.False(presentation.Observe(true));
        Assert.False(presentation.Observe(false));
        Assert.True(presentation.Observe(true));
    }

    private static TournamentSessionSnapshot CreateSnapshot(TournamentSessionPhase phase)
        => new(
            "session-a",
            "mission-a",
            "town-a",
            "arena-a",
            "prize-a",
            phase,
            1,
            1,
            "match-a",
            "host-a",
            Array.Empty<string>(),
            Array.Empty<TournamentContestantData>(),
            Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            Array.Empty<TournamentRoundData>(),
            0,
            0,
            0,
            true,
            false,
            null);
}
