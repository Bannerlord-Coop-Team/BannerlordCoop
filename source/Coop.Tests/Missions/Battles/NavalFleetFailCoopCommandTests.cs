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
    [Fact]
    public void ExtraArguments_ShowUsage()
    {
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(1);

        var result = new NavalFleetFailCoopCommand().ProcessCommand(args.Object);

        Assert.False(result.Succeeded);
        Assert.Equal("Usage: coop.debug.naval.fleet_fail", result.Output);
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
