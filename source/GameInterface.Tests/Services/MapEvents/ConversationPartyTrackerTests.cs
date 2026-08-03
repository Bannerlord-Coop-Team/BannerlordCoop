using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class ConversationPartyTrackerTests
{
    [Fact]
    public void RefreshingSameEngagement_RecordsServerDetectedDefender()
    {
        var tracker = new ConversationPartyTracker(new Mock<IObjectManager>().Object);
        var peer = new object();

        Assert.True(tracker.TryBeginEngagement(peer, "player-party", "bandit-party", false));
        Assert.True(tracker.TryBeginEngagement(peer, "player-party", "bandit-party", true, engagerIsDefender: true));
        Assert.True(tracker.TryGetEngagement(peer, out var engagement));
        Assert.True(engagement.EngagerIsDefender);
        Assert.False(engagement.WasAiDisabled);

        Assert.True(tracker.TryEndEngagement(peer, out _, out _, out _));
        tracker.Dispose();
    }

    // A held party belongs to exactly one player. Server-side conversation outcomes are authorised
    // only as "this peer holds an engagement with this party", so a shared hold let two players each
    // apply the same one-shot result - two recruiters persuading one lord, both paying, the lord
    // defecting twice.
    [Fact]
    public void TryBeginEngagement_WhenPartyEngagedByAnotherPlayer_Fails()
    {
        var tracker = new ConversationPartyTracker(new Mock<IObjectManager>().Object);
        var firstPlayer = new object();
        var secondPlayer = new object();

        Assert.True(tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false));
        Assert.False(tracker.TryBeginEngagement(secondPlayer, "player2", "lord1", wasAiDisabled: true));
        Assert.False(tracker.TryGetEngagement(secondPlayer, out _));

        // The holder still owns it, and releasing frees the party for the next player.
        Assert.True(tracker.TryEndEngagement(firstPlayer, out _, out _, out var shouldRelease));
        Assert.True(shouldRelease);
        Assert.True(tracker.TryBeginEngagement(secondPlayer, "player2", "lord1", wasAiDisabled: false));

        Assert.True(tracker.TryEndEngagement(secondPlayer, out _, out _, out _));
        tracker.Dispose();
    }
}
