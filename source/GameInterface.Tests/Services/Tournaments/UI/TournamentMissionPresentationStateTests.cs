using GameInterface.Services.Tournaments.UI;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments.UI;

public class TournamentMissionPresentationStateTests
{
    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, false, false)]
    public void ShouldShowCombatUi_HidesLiveOverlayForMovableSpectator(
        bool bracketVisible,
        bool matchActive,
        bool hasMovableSpectator,
        bool expected)
    {
        Assert.Equal(
            expected,
            TournamentMissionPresentationState.ShouldShowCombatUi(
                bracketVisible,
                matchActive,
                hasMovableSpectator));
    }
}
