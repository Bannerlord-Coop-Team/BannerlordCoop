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
    [InlineData("ally", "enemy")]
    public void BadArguments_ShowUsage(params string[] values)
    {
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(values.Length);
        args.Setup(value => value[It.IsAny<int>()]).Returns((int index) => values[index]);

        var result = new NavalFleetFailCoopCommand().ProcessCommand(args.Object);

        Assert.False(result.Succeeded);
        Assert.Equal("Usage: coop.debug.naval.fleet_fail [enemy|ally] [before|after_hull|crewing]", result.Output);
    }

    // At most one side word and one stage word, in either order, pass the argument check and only then need the mission.
    [Theory]
    [InlineData("before")]
    [InlineData("AFTER_HULL")]
    [InlineData("ally")]
    [InlineData("crewing", "Ally")]
    [InlineData("enemy", "before")]
    public void SideAndStageWords_PassTheArgumentCheck(params string[] values)
    {
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(values.Length);
        args.Setup(value => value[It.IsAny<int>()]).Returns((int index) => values[index]);

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
