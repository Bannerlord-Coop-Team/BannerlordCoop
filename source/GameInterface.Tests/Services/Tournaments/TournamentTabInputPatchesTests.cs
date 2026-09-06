using GameInterface.Services.Tournaments.Patches;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentTabInputPatchesTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ShouldRunScoreboardTick_BlocksEntireScoreboardDuringCoopTournament(
        bool isCoopTournamentMissionActive,
        bool expected)
    {
        Assert.Equal(
            expected,
            TournamentScoreboardInputPatches.ShouldRunScoreboardTick(isCoopTournamentMissionActive));
    }
}
