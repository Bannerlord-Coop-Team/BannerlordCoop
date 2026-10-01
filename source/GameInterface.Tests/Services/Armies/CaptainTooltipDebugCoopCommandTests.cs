#if DEBUG
using Common.Commands;
using GameInterface.Services.Armies.Commands;
using Xunit;

namespace GameInterface.Tests.Services.Armies;

[Collection("CampaignCurrentCollection")]
public class CaptainTooltipDebugCoopCommandTests
{
    [Theory]
    [InlineData("input")]
    [InlineData("show")]
    [InlineData("hide")]
    [InlineData("observe", "captain", "extra")]
    public void RejectsUnsupportedActionsAndMissingCaptain(params string[] values)
    {
        var command = new CaptainTooltipDebugCoopCommand();
        var result = command.ProcessCommand(new CoopCommandArgsFactory().FromValues(values));

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
        Assert.Contains("Expected observe", result.Output);
    }

    [Fact]
    public void ObserveWithoutDeploymentFailsExplicitly()
    {
        var result = new CaptainTooltipDebugCoopCommand().ProcessCommand(
            new CoopCommandArgsFactory().FromValues(new[] { "observe" }));

        Assert.False(result.Succeeded);
        Assert.Contains("deployment view is required", result.Output);
    }
}
#endif
