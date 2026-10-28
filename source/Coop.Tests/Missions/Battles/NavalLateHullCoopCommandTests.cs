#if COOP_NAVAL
using Common.Commands;
using Missions.Naval;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks malformed station cheats cannot reach the mission probe.</summary>
public class NavalLateHullCoopCommandTests
{
    /// <summary>Rejects incomplete actions, invalid indices and ambiguous extra release arguments.</summary>
    [Theory]
    [InlineData("a climb")]
    [InlineData("a climb -1")]
    [InlineData("a rope 1 -1")]
    [InlineData("a oar 2147483648")]
    [InlineData("a release 1 0")]
    [InlineData("a throw 1")]
    [InlineData("a climb 1 0 extra")]
    public void ProcessCommand_RejectsMalformedActionsBeforeMutation(string line)
    {
        var values = line.Split(' ');
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            int captured = index;
            args.SetupGet(value => value[captured]).Returns(values[index]);
        }
        var probe = new Mock<INavalLateHullProbe>(MockBehavior.Strict);

        var result = new NavalLateHullCoopCommand(probe.Object).ProcessCommand(args.Object);

        Assert.False(result.Succeeded);
        Assert.Equal(NavalLateHullCoopCommand.Usage, result.Output);
        probe.VerifyNoOtherCalls();
    }

    /// <summary>Reads the index forms used by live tooling and the inspection that survives hull removal.</summary>
    [Theory]
    [InlineData("", null, -1, 0)]
    [InlineData("a", null, -1, 0)]
    [InlineData("a CLIMB 0", "climb", 0, 0)]
    [InlineData("a rope 7 2", "rope", 7, 2)]
    [InlineData("a oar 7 3", "oar", 7, 3)]
    [InlineData("a release 7", "release", 7, 0)]
    public void TryParse_ReadsInspectionAndStationActions(string line, string? operation, int agent, int station)
    {
        Assert.True(NavalLateHullCoopCommand.TryParse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            out _, out var parsedOperation, out int parsedAgent, out int parsedStation));
        Assert.Equal(operation, parsedOperation);
        Assert.Equal(agent, parsedAgent);
        Assert.Equal(station, parsedStation);
    }
}
#endif
