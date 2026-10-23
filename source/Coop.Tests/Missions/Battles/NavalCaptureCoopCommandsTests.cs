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
    [InlineData("status", "force")]
    [InlineData("force", "1d93")]
    [InlineData("1d93", "force", "force")]
    [InlineData("1d93", "twice", "force")]
    [InlineData("status", "force", "twice")]
    [InlineData("1d93", "force", "twice", "twice")]
    public void Capture_WithBadArguments_ShowsTheUsage(params string[] args)
    {
        var result = new NavalCaptureCoopCommand().ProcessCommand(Args(args));

        Assert.False(result.Succeeded);
        Assert.Equal(NavalCaptureCoopCommand.Usage, result.Output);
    }

    [Theory]
    [InlineData("1d93")]
    [InlineData("status", "1d93")]
    [InlineData("1d93", "force")]
    [InlineData("1d93", "FORCE")]
    [InlineData("1d93", "force", "twice")]
    public void Capture_OutsideANavalBattle_Fails(params string[] args)
    {
        var result = new NavalCaptureCoopCommand().ProcessCommand(Args(args));

        Assert.False(result.Succeeded);
        Assert.Equal("No active coop naval battle mission.", result.Output);
    }

    // R46: capture status takes two arguments, which the old one-argument definition turned away before the command ran.
    [Theory]
    [InlineData(new[] { "1d93" }, NavalCaptureCoopCommand.CaptureMode.Helm, "1d93")]
    [InlineData(new[] { "status", "1d93" }, NavalCaptureCoopCommand.CaptureMode.Status, "1d93")]
    [InlineData(new[] { "1d93", "force" }, NavalCaptureCoopCommand.CaptureMode.Force, "1d93")]
    [InlineData(new[] { "1d93", "force", "TWICE" }, NavalCaptureCoopCommand.CaptureMode.ForceTwice, "1d93")]
    public void Capture_ParsesEachForm(string[] args, NavalCaptureCoopCommand.CaptureMode mode, string prefix)
    {
        Assert.True(NavalCaptureCoopCommand.TryParse(Args(args), out var parsedMode, out var parsedPrefix));
        Assert.Equal(mode, parsedMode);
        Assert.Equal(prefix, parsedPrefix);
    }

    // R47 L7: force twice takes a third argument, which the registry turns away unless it is declared.
    [Fact]
    public void Capture_DeclaresOptionalSecondAndThirdArguments_SoTheRegistryLetsStatusAndForceTwiceThrough()
    {
        var expected = new NavalCaptureCoopCommand().ExpectedArgs;

        Assert.Equal(3, expected.Length);
        Assert.True(expected[0].IsRequired);
        Assert.False(expected[1].IsRequired);
        Assert.False(expected[2].IsRequired);
    }

    // R46 L3: an orphan has no team, so only the registry's orphan flag and authority make it this client's to take back.
    [Theory]
    [InlineData(true, "local", true)]
    [InlineData(true, "other", false)]
    [InlineData(false, "local", false)]
    public void IsOwnOrphan_OnlyThisClientsOrphan(bool orphaned, string authority, bool expected)
    {
        var info = new global::Missions.Battles.NetworkShipInfo(System.Guid.NewGuid(), authority, "party", false,
            ShipTestHulls.Create(), null);
        if (orphaned) info.MarkOrphaned(TaleWorlds.Core.BattleSideEnum.Attacker, 1);

        Assert.Equal(expected, NavalCaptureCoopCommand.IsOwnOrphan(info, "local"));
    }

    // R46 review: force reports where the request went, read back from the replicator after publishing the timer's event.
    [Theory]
    [InlineData(true, "request sent to host")]
    [InlineData(false, "decided here")]
    public void ForceOutcome_NamesWhereTheRequestWent(bool waiting, string expected)
    {
        Assert.StartsWith(expected, NavalCaptureCoopCommand.ForceOutcome(waiting, "host"));
    }

    [Fact]
    public void Swim_WithBadArguments_ShowsTheUsage_AndOutsideABattleFails()
    {
        var command = new NavalSwimCoopCommand();

        Assert.Equal(NavalSwimCoopCommand.Usage, command.ProcessCommand(Args()).Output);
        Assert.Equal("No active coop naval battle mission.", command.ProcessCommand(Args("1d93")).Output);
    }

    [Fact]
    public void Swim_LandsClearOfTheStarboardSide_MidwayAlongTheHull()
    {
        var offset = NavalSwimCoopCommand.SideOffset(new TaleWorlds.Library.Vec2(-3f, -10f), new TaleWorlds.Library.Vec2(3f, 14f));

        Assert.Equal(3f + NavalSwimCoopCommand.SideClearance, offset.x, 3);
        Assert.Equal(2f, offset.y, 3);
        Assert.Equal(0f, offset.z, 3);
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
