#if DEBUG
using GameInterface.Services.UI.Patches;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class ScreenManagerRobustnessPatchesTests
{
    [Theory]
    [InlineData(true, true, 1, true)]
    [InlineData(false, true, 1, false)]
    [InlineData(true, false, 1, false)]
    [InlineData(true, true, 0, false)]
    public void ShouldPumpGameThread_RequiresLiveTestGameThreadWithQueuedWork(
        bool isLiveTestRun,
        bool isGameThread,
        int queueLength,
        bool expected)
    {
        Assert.Equal(
            expected,
            ScreenManagerRobustnessPatches.ShouldPumpGameThread(isLiveTestRun, isGameThread, queueLength));
    }

    [Theory]
    [InlineData("/cooptestrun", true)]
    [InlineData("/COOPTESTRUN", true)]
    [InlineData("/cooptestrun=token", false)]
    [InlineData("token", false)]
    public void HasLiveTestRunArgument_RequiresExactArgument(string argument, bool expected)
    {
        Assert.Equal(expected, ScreenManagerRobustnessPatches.HasLiveTestRunArgument(new[] { argument }));
    }
}
#endif
