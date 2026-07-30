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

    [Fact]
    public void StaleRequestCannotEndRefreshedEngagement()
    {
        var tracker = new ConversationPartyTracker(new Mock<IObjectManager>().Object);
        var peer = new object();

        Assert.True(tracker.TryBeginEngagement(
            peer,
            "player-party",
            "bandit-party",
            false,
            requestId: "older-request"));
        Assert.True(tracker.TryBeginEngagement(
            peer,
            "player-party",
            "bandit-party",
            true,
            requestId: "current-request"));

        Assert.False(tracker.TryEndEngagement(
            peer,
            out _,
            out _,
            out _,
            expectedRequestId: "older-request",
            requireRequestIdMatch: true));
        Assert.True(tracker.TryGetEngagement(peer, out var currentEngagement));
        Assert.Equal("current-request", currentEngagement.RequestId);

        Assert.True(tracker.TryEndEngagement(
            peer,
            out _,
            out _,
            out _,
            expectedRequestId: "current-request",
            requireRequestIdMatch: true));
        tracker.Dispose();
    }
}
