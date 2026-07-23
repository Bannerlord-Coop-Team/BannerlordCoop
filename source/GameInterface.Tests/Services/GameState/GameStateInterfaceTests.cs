using GameInterface.Services.GameState.Interfaces;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

public class GameStateInterfaceTests
{
    [Theory]
    [InlineData("/autoconnect")]
    [InlineData("/AUTOCONNECT")]
    public void IsAutoConnectLaunch_DetectsExactArgumentCaseInsensitively(string argument)
    {
        Assert.True(GameStateInterface.IsAutoConnectLaunch(
            new[] { "Bannerlord.exe", "/server", argument }));
    }

    [Theory]
    [InlineData("/server")]
    [InlineData("prefix/autoconnect")]
    [InlineData("/autoconnect-extra")]
    public void IsAutoConnectLaunch_RejectsMissingOrPartialArguments(string argument)
    {
        Assert.False(GameStateInterface.IsAutoConnectLaunch(
            new[] { "Bannerlord.exe", argument }));
    }
}
