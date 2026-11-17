#if COOP_NAVAL && DEBUG
using Common.Commands;
using Missions.Naval;
using Moq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks the fleet failure command's console boundary before a mission is available.</summary>
[Collection("Mission.Current")]
public class NavalFleetFailCoopCommandTests
{
    // Bad arguments must not reach the mission or arm anything.
    [Theory]
    [InlineData("later")]
    [InlineData("")]
    [InlineData("before", "crewing")]
    public void BadArguments_ShowUsage(params string[] values)
    {
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(values.Length);
        args.Setup(value => value[It.IsAny<int>()]).Returns((int index) => values[index]);

        var result = new NavalFleetFailCoopCommand().ProcessCommand(args.Object);

        Assert.False(result.Succeeded);
        Assert.Equal("Usage: coop.debug.naval.fleet_fail [before|after_hull|crewing]", result.Output);
    }

    // A known stage word passes the argument check and only then needs the mission.
    [Theory]
    [InlineData("before")]
    [InlineData("AFTER_HULL")]
    [InlineData("crewing")]
    public void StageWord_PassesTheArgumentCheck(string stage)
    {
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(1);
        args.Setup(value => value[0]).Returns(stage);

        var result = new NavalFleetFailCoopCommand().ProcessCommand(args.Object);

        Assert.Equal("No active coop naval battle mission.", result.Output);
    }

    // Arming outside a naval mission must fail instead of persisting into a later battle.
    [Fact]
    public void OutsideNavalMission_RefusesArming()
    {
        var result = new NavalFleetFailCoopCommand().ProcessCommand(Mock.Of<ICoopCommandArgs>());

        Assert.False(result.Succeeded);
        Assert.Equal("No active coop naval battle mission.", result.Output);
    }
}
#endif
