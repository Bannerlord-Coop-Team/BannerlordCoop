using Coop.Core.Common.Session;
using System;
using Xunit;

namespace Coop.Tests.Session;

public class JoinAttemptPresentationTests
{
    [Fact]
    public void For_DirectJoin_DoesNotExposeTheServerEndpoint()
    {
        var presentation = JoinAttemptPresentation.For(JoinIntent.PlayerDirect);

        Assert.Equal("Contacting the co-op server...", presentation.Description);
        Assert.Equal("Cancel", presentation.CancelLabel);
    }

    [Fact]
    public void For_SteamJoin_DescribesTheSteamRoute()
    {
        var presentation = JoinAttemptPresentation.For(JoinIntent.PlayerSteam);

        Assert.Equal("Contacting the host through Steam...", presentation.Description);
    }

    [Fact]
    public void For_HostLoopback_SaysTheServerOutlivesTheCancelledWait()
    {
        var presentation = JoinAttemptPresentation.For(JoinIntent.HostLoopback);

        Assert.Contains("stays open", presentation.CancelledNotice);
        Assert.NotEqual("Cancel", presentation.CancelLabel);
    }

    [Fact]
    public void For_PlayerJoins_KeepTheTitleThePostConnectStatesUse()
    {
        // MainMenuState shows this title once connected; matching it means the heading does not
        // change under the player when the handshake lands.
        Assert.Equal("Connecting to Coop Server",
            JoinAttemptPresentation.For(JoinIntent.PlayerDirect).Title);
        Assert.Equal("Connecting to Coop Server",
            JoinAttemptPresentation.For(JoinIntent.PlayerSteam).Title);
    }

    [Fact]
    public void For_EveryDefinedIntent_ProvidesCompleteCopy()
    {
        foreach (JoinIntent intent in Enum.GetValues(typeof(JoinIntent)))
        {
            var presentation = JoinAttemptPresentation.For(intent);

            Assert.False(string.IsNullOrWhiteSpace(presentation.Title), $"{intent} title");
            Assert.False(string.IsNullOrWhiteSpace(presentation.Description), $"{intent} description");
            Assert.False(string.IsNullOrWhiteSpace(presentation.CancelLabel), $"{intent} cancel label");
            Assert.False(string.IsNullOrWhiteSpace(presentation.CancelledNotice), $"{intent} notice");
        }
    }
}
