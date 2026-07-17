using GameInterface.Services.Tournaments.Patches;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentTabInputPatchesTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShouldSuppress_OnlyBlocksTabDuringCoopTournamentMission(
        bool isTab,
        bool isCoopTournamentMissionActive,
        bool expected)
    {
        Assert.Equal(
            expected,
            TournamentTabInputPatches.ShouldSuppress(isTab, isCoopTournamentMissionActive));
    }
}