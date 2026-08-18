using Coop.Core.Common.Session;
using System;
using Xunit;

namespace Coop.Tests.Session;

public class JoinAttemptPresentationTests
{
    [Fact]
    public void For_DirectJoin_NamesTheEndpointBeingDialled()
    {
        var presentation = JoinAttemptPresentation.For(JoinIntent.PlayerDirect, "203.0.113.7", 4200);

        Assert.Equal("Contacting 203.0.113.7:4200...", presentation.Description);
        Assert.Equal("Cancel", presentation.CancelLabel);
    }

    [Fact]
    public void For_ProviderJoin_KeepsTheTunnelEndpointOutOfTheDescription()
    {
        var presentation = JoinAttemptPresentation.For(JoinIntent.PlayerProvider, "127.0.0.1", 27015);

        Assert.DoesNotContain("127.0.0.1", presentation.Description);
        Assert.DoesNotContain("27015", presentation.Description);
        Assert.DoesNotContain("Steam", presentation.Description);
        Assert.DoesNotContain("GOG", presentation.Description);
    }

    [Fact]
    public void For_HostLoopback_SaysTheServerOutlivesTheCancelledWait()
    {
        var presentation = JoinAttemptPresentation.For(JoinIntent.HostLoopback, "127.0.0.1", 4200);

        Assert.Contains("stays open", presentation.CancelledNotice);
        Assert.NotEqual("Cancel", presentation.CancelLabel);
    }

    [Fact]
    public void For_PlayerJoins_KeepTheTitleThePostConnectStatesUse()
    {
        // MainMenuState shows this title once connected; matching it means the heading does not
        // change under the player when the handshake lands.
        Assert.Equal("Connecting to Coop Server",
            JoinAttemptPresentation.For(JoinIntent.PlayerDirect, "localhost", 4200).Title);
        Assert.Equal("Connecting to Coop Server",
            JoinAttemptPresentation.For(JoinIntent.PlayerProvider, "localhost", 4200).Title);
    }

    [Fact]
    public void For_EveryDefinedIntent_ProvidesCompleteCopy()
    {
        foreach (JoinIntent intent in Enum.GetValues(typeof(JoinIntent)))
        {
            var presentation = JoinAttemptPresentation.For(intent, "localhost", 4200);

            Assert.False(string.IsNullOrWhiteSpace(presentation.Title), $"{intent} title");
            Assert.False(string.IsNullOrWhiteSpace(presentation.Description), $"{intent} description");
            Assert.False(string.IsNullOrWhiteSpace(presentation.CancelLabel), $"{intent} cancel label");
            Assert.False(string.IsNullOrWhiteSpace(presentation.CancelledNotice), $"{intent} notice");
        }
    }
}
