#if DEBUG
using Common.Commands;
using GameInterface.Services.MapEvents.Commands;
using System;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.Commands;

public class BattleResultsCommandsTests
{
    [Theory]
    [InlineData(typeof(BattleResultsScoreboardCoopCommand), "scoreboard")]
    [InlineData(typeof(BattleResultsLeaveCoopCommand), "leave_results")]
    public void Commands_LiveInTheMapEventNamespaceOnClients(Type commandType, string name)
    {
        var command = (ICoopCommand)Activator.CreateInstance(commandType);

        Assert.Equal("coop.debug.map_event", command.Prefix);
        Assert.Equal(name, command.Name);
        Assert.Equal(CoopCommandSide.Client, command.Side);
    }

    [Theory]
    [InlineData(typeof(BattleResultsScoreboardCoopCommand))]
    [InlineData(typeof(BattleResultsLeaveCoopCommand))]
    public void ProcessCommand_WithoutMission_RefusesBeforeTouchingTheUi(Type commandType)
    {
        var command = (ICoopCommand)Activator.CreateInstance(commandType);

        var result = command.ProcessCommand(new CoopCommandArgsFactory().FromValues(Array.Empty<string>()));

        Assert.False(result.Succeeded);
        Assert.Equal("No active battle mission.", result.Output);
    }
}
#endif
