#if DEBUG
using Common.Commands;
using Missions.Naval;
using Moq;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R47 L9: the DEBUG order command reads its three orders and refuses before touching any formation outside a battle.</summary>
[Collection("Mission.Current")]
public class NavalOrderCoopCommandTests
{
    [Theory]
    [InlineData("followme", OrderType.FollowMe)]
    [InlineData("CHARGE", OrderType.Charge)]
    [InlineData(" hold ", OrderType.StandYourGround)]
    public void TryParseOrder_ReadsTheOrderMenusOrders(string value, OrderType expected)
    {
        Assert.True(NavalOrderCoopCommand.TryParseOrder(value, out var orderType));
        Assert.Equal(expected, orderType);
    }

    [Theory]
    [InlineData()]
    [InlineData("retreat")]
    [InlineData("hold", "charge")]
    public void Order_WithBadArguments_ShowsTheUsage(params string[] args)
    {
        var result = new NavalOrderCoopCommand().ProcessCommand(Args(args));

        Assert.False(result.Succeeded);
        Assert.Equal(NavalOrderCoopCommand.Usage, result.Output);
    }

    [Fact]
    public void Order_OutsideANavalBattle_Fails()
    {
        var result = new NavalOrderCoopCommand().ProcessCommand(Args("followme"));

        Assert.False(result.Succeeded);
        Assert.Equal("No active coop naval battle mission.", result.Output);
    }

    // Command arguments over a fixed list.
    private static ICoopCommandArgs Args(params string[] values)
    {
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(a => a.Count).Returns(values.Length);
        args.Setup(a => a[It.IsAny<int>()]).Returns((int index) => values[index]);
        return args.Object;
    }
}
#endif
