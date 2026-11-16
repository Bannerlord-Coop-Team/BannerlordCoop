using GameInterface.Services.UI.Patches;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class FinishedSimulationScoreboardTickPatchTests
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    public void ShouldContinueTick_UsesSimulationCompletionAndEventLifetime(
        bool isSimulation,
        bool isOver,
        bool eventStillLive,
        bool expected)
    {
        Assert.Equal(expected,
            FinishedSimulationScoreboardTickPatch.ShouldContinueTick(isSimulation, isOver, eventStillLive));
    }
}
