using GameInterface.Services.UI.Patches;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class FinishedSimulationScoreboardTickPatchTests
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, true, false)]
    public void ShouldContinueTick_SkipsOnlyFinishedSimulations(
        bool isSimulation,
        bool isOver,
        bool hasMapEvent,
        bool expected)
    {
        Assert.Equal(
            expected,
            FinishedSimulationScoreboardTickPatch.ShouldContinueTick(isSimulation, isOver, hasMapEvent));
    }
}
