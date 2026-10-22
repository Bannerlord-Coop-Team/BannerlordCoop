#if DEBUG
using Common.Commands;
using Missions.Naval;
using Moq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>The DEBUG capture commands refuse bad arguments and a client with no co-op naval battle before touching any hull.</summary>
[Collection("Mission.Current")]
public class NavalCaptureCoopCommandsTests
{
    [Theory]
    [InlineData()]
    [InlineData("status")]
    [InlineData("1d93", "2c11")]
    [InlineData("status", "1d93", "2c11")]
    public void Capture_WithBadArguments_ShowsTheUsage(params string[] args)
    {
        var result = new NavalCaptureCoopCommand().ProcessCommand(Args(args));

        Assert.False(result.Succeeded);
        Assert.Equal(NavalCaptureCoopCommand.Usage, result.Output);
    }

    [Theory]
    [InlineData("1d93")]
    [InlineData("status", "1d93")]
    public void Capture_OutsideANavalBattle_Fails(params string[] args)
    {
        var result = new NavalCaptureCoopCommand().ProcessCommand(Args(args));

        Assert.False(result.Succeeded);
        Assert.Equal("No active coop naval battle mission.", result.Output);
    }

    [Fact]
    public void ClearCrew_WithBadArguments_ShowsTheUsage_AndOutsideABattleFails()
    {
        var command = new NavalClearCrewCoopCommand();

        var usage = command.ProcessCommand(Args());
        var noBattle = command.ProcessCommand(Args("1d93"));

        Assert.Equal(NavalClearCrewCoopCommand.Usage, usage.Output);
        Assert.Equal("No active coop naval battle mission.", noBattle.Output);
    }

    // The command arguments as the console splits them.
    private static ICoopCommandArgs Args(params string[] values)
    {
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(a => a.Count).Returns(values.Length);
        args.Setup(a => a[It.IsAny<int>()]).Returns((int index) => values[index]);
        return args.Object;
    }
}
#endif
