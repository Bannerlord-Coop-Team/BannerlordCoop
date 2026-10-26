#if COOP_NAVAL
using Common.Commands;
using Missions.Battles;
using Missions.Naval;
using Moq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks malformed burn requests cannot reach hull mutation.</summary>
public class NavalBurnCoopCommandTests
{
    /// <summary>Rejects missing, blank or excess arguments before accessing the mission or damage engine.</summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(1, " ")]
    [InlineData(2, "72da8e4a")]
    public void ProcessCommand_RejectsMalformedArguments(int count, string? prefix)
    {
        var engine = new Mock<INavalShipEngine>(MockBehavior.Strict);
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(count);
        args.SetupGet(value => value[0]).Returns(prefix!);

        var result = new NavalBurnCoopCommand(engine.Object).ProcessCommand(args.Object);

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
        Assert.Equal("Usage: coop.debug.naval.burn <shipId-prefix>", result.Output);
        engine.VerifyNoOtherCalls();
    }
}
#endif
