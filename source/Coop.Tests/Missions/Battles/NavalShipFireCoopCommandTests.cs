#if DEBUG
using Common.Commands;
using Missions.Naval;
using Moq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R47 ballista: the DEBUG fire command reads its forms and only shoots where vanilla's shot can run.</summary>
[Collection("Mission.Current")]
public class NavalShipFireCoopCommandTests
{
    [Theory]
    [InlineData(new[] { "5d5b", "e4f7" }, false)]
    [InlineData(new[] { "5d5b", "e4f7", "STATUS" }, true)]
    public void TryParse_ReadsFireAndStatus(string[] args, bool statusOnly)
    {
        Assert.True(NavalShipFireCoopCommand.TryParse(Args(args), out var weapon, out var target, out bool parsedStatus));
        Assert.Equal("5d5b", weapon);
        Assert.Equal("e4f7", target);
        Assert.Equal(statusOnly, parsedStatus);
    }

    [Theory]
    [InlineData()]
    [InlineData("5d5b")]
    [InlineData("5d5b", "e4f7", "now")]
    [InlineData("5d5b", "e4f7", "status", "status")]
    public void Fire_WithBadArguments_ShowsTheUsage(params string[] args)
    {
        var result = new NavalShipFireCoopCommand().ProcessCommand(Args(args));

        Assert.False(result.Succeeded);
        Assert.Equal(NavalShipFireCoopCommand.Usage, result.Output);
    }

    [Fact]
    public void Fire_OutsideANavalBattle_Fails()
    {
        var result = new NavalShipFireCoopCommand().ProcessCommand(Args("5d5b", "e4f7"));

        Assert.False(result.Succeeded);
        Assert.Equal("No active coop naval battle mission.", result.Output);
    }

    // Copies have Shoot blocked, Shoot records the gunner as the shooter, and only a loaded (Idle) weapon shoots.
    [Theory]
    [InlineData(true, true, true, true, null)]
    [InlineData(false, true, true, true, "simulated by host")]
    [InlineData(true, false, true, true, "simulated by host")]
    [InlineData(true, true, false, true, "nobody mans")]
    [InlineData(true, true, true, false, "not loaded")]
    public void FireRefusal_NeedsTheSimulatingClientAGunnerAndALoadedWeapon(bool ownsHull, bool simulatesMachine, bool hasGunner,
        bool isLoaded, string expected)
    {
        var refusal = NavalShipFireCoopCommand.FireRefusal(ownsHull, simulatesMachine, hasGunner, isLoaded, "host");

        if (expected == null) Assert.Null(refusal);
        else Assert.Contains(expected, refusal);
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
