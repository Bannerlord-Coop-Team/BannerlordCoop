#if COOP_NAVAL && DEBUG
using Common;
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
    [InlineData("crewing", "ally")]
    public void BadArguments_ShowUsage(params string[] values)
    {
        var result = new NavalFleetFailCoopCommand().ProcessCommand(Args(values));

        Assert.False(result.Succeeded);
        Assert.Equal("Usage: coop.debug.naval.fleet_fail [enemy|ally] [before|after_hull|crewing]", result.Output);
    }

    // The usage by position: an optional side word, then an optional stage word, each with its default when left out.
    [Theory]
    [InlineData(false, NavalFleetFailStage.BeforeSpawn)]
    [InlineData(false, NavalFleetFailStage.AfterFirstHull, "AFTER_HULL")]
    [InlineData(true, NavalFleetFailStage.BeforeSpawn, "ally")]
    [InlineData(true, NavalFleetFailStage.DuringCrewing, "Ally", "crewing")]
    [InlineData(false, NavalFleetFailStage.DuringCrewing, "enemy", "crewing")]
    public void TryParse_ReadsAnOptionalSideThenAnOptionalStage(bool expectedAlly, NavalFleetFailStage expectedStage,
        params string[] values)
    {
        Assert.True(NavalFleetFailCoopCommand.TryParse(Args(values), out bool ally, out var stage));

        Assert.Equal((expectedAlly, expectedStage), (ally, stage));
    }

    // Arming outside a naval mission must fail instead of persisting into a later battle.
    [Fact]
    public void OutsideNavalMission_RefusesArming()
    {
        var result = new NavalFleetFailCoopCommand().ProcessCommand(Mock.Of<ICoopCommandArgs>());

        Assert.False(result.Succeeded);
        Assert.Equal("No active coop naval battle mission.", result.Output);
    }

    // The console route checks the words against the declared arguments before the command parses them.
    [Theory]
    [InlineData("ally", "before")]
    [InlineData("ally")]
    [InlineData("crewing")]
    public void ConsoleRoute_PassesTheSideAndStageWordsToTheCommand(params string[] values)
    {
        var registry = new CoopCommandRegistry(new ICoopCommand[] { new NavalFleetFailCoopCommand() },
            new Serilog.LoggerConfiguration().CreateLogger());
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;
        try
        {
            var result = registry.ProcessCommand("coop.debug.naval.fleet_fail", new CoopCommandArgsFactory().FromValues(values));

            Assert.Equal("No active coop naval battle mission.", result.Output);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // Console arguments holding the given words.
    private static ICoopCommandArgs Args(string[] values)
    {
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(values.Length);
        args.Setup(value => value[It.IsAny<int>()]).Returns((int index) => values[index]);
        return args.Object;
    }
}
#endif
