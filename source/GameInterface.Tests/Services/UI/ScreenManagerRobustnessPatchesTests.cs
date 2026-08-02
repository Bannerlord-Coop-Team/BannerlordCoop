#if DEBUG
using GameInterface.Services.UI.Patches;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class ScreenManagerRobustnessPatchesTests
{
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
