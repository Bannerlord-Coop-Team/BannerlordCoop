using GameInterface.Services.UI.Patches;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class FinishedSimulationScoreboardTickPatchTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void ShouldContinueTick_SkipsOnlyFinishedSimulations(
        bool isSimulation,
        bool isOver,
        bool expected)
    {
        Assert.Equal(
            expected,
            FinishedSimulationScoreboardTickPatch.ShouldContinueTick(isSimulation, isOver));
    }
}
