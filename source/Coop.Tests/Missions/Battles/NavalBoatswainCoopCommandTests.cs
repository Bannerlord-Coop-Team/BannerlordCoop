#if DEBUG && COOP_NAVAL
using Common;
using Common.Commands;
using Coop.Naval.Commands;
using GameInterface.Services.ObjectManager;
using Moq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks invalid fixture requests cannot reach campaign state or mutate a client's skills.</summary>
public class NavalBoatswainCoopCommandTests
{
    /// <summary>Rejects malformed arguments before any registry access.</summary>
    [Theory]
    [InlineData()]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("MobileParty_Player", "prep")]
    [InlineData("MobileParty_Player", "prepare", "extra")]
    public void ProcessCommand_RejectsMalformedRequests(params string[] values)
    {
        var objects = new Mock<IObjectManager>(MockBehavior.Strict);
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(values.Length);
        args.Setup(value => value[It.IsAny<int>()]).Returns((int index) => values[index]);

        var result = new NavalBoatswainCoopCommand(objects.Object).ProcessCommand(args.Object);

        Assert.False(result.Succeeded);
        Assert.Equal("Usage: coop.debug.naval.boatswain <partyId> [prepare]", result.Output);
        objects.VerifyNoOtherCalls();
    }

    /// <summary>Refuses the mutating mode on a client even when no campaign is loaded.</summary>
    [Fact]
    public void ProcessCommand_ClientCannotPrepare()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;
        try
        {
            var objects = new Mock<IObjectManager>(MockBehavior.Strict);
            var args = new Mock<ICoopCommandArgs>();
            args.SetupGet(value => value.Count).Returns(2);
            args.Setup(value => value[0]).Returns("MobileParty_Player");
            args.Setup(value => value[1]).Returns("prepare");

            var result = new NavalBoatswainCoopCommand(objects.Object).ProcessCommand(args.Object);

            Assert.False(result.Succeeded);
            Assert.Equal("Run prepare on the server before battle.", result.Output);
            objects.VerifyNoOtherCalls();
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
#endif