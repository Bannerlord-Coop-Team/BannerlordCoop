using GameInterface.Services.Locations.Conversations;
using Xunit;

namespace Coop.Tests.GameInterface.Services.Locations;

public class LocationConversationTrackerTests
{
    private readonly LocationConversationTracker tracker = new LocationConversationTracker(objectManager: null);

    private readonly object firstPlayer = new object();
    private readonly object secondPlayer = new object();

    private static readonly string Notable = LocationConversationTracker.ComposeKey("town_ES1_lordshall", "notable1");
    private static readonly string OtherNotable = LocationConversationTracker.ComposeKey("town_ES1_lordshall", "notable2");

    [Fact]
    public void TryBeginEngagement_WhenNpcUnengaged_BeginsEngagement()
    {
        var began = tracker.TryBeginEngagement(firstPlayer, Notable);

        Assert.True(began);
        Assert.True(tracker.IsEngagedByOther(Notable, secondPlayer));
        Assert.False(tracker.IsEmpty);
    }

    [Fact]
    public void TryBeginEngagement_WhenNpcEngagedByOtherPlayer_Fails()
    {
        tracker.TryBeginEngagement(firstPlayer, Notable);

        var began = tracker.TryBeginEngagement(secondPlayer, Notable);

        Assert.False(began);
        // Still held by the first player.
        Assert.True(tracker.IsEngagedByOther(Notable, secondPlayer));
        Assert.False(tracker.IsEngagedByOther(Notable, firstPlayer));
    }

    [Fact]
    public void TryBeginEngagement_WhileEngagedWithDifferentNpc_Fails()
    {
        // First approval wins: a live engagement must not be superseded by a later request.
        tracker.TryBeginEngagement(firstPlayer, Notable);

        var began = tracker.TryBeginEngagement(firstPlayer, OtherNotable);

        Assert.False(began);
        Assert.True(tracker.IsEngagedByOther(Notable, secondPlayer));
        Assert.False(tracker.IsEngagedByOther(OtherNotable, secondPlayer));
    }

    [Fact]
    public void TryBeginEngagement_SameNpcAgain_Refreshes()
    {
        tracker.TryBeginEngagement(firstPlayer, Notable);

        var began = tracker.TryBeginEngagement(firstPlayer, Notable);

        Assert.True(began);
    }

    [Fact]
    public void TryBeginEngagement_AfterEndingPreviousEngagement_Succeeds()
    {
        tracker.TryBeginEngagement(firstPlayer, Notable);
        tracker.TryEndEngagement(firstPlayer, out _);

        var began = tracker.TryBeginEngagement(firstPlayer, OtherNotable);

        Assert.True(began);
        Assert.False(tracker.IsEngagedByOther(Notable, secondPlayer));
        Assert.True(tracker.IsEngagedByOther(OtherNotable, secondPlayer));
    }

    [Fact]
    public void TryEndEngagement_ReturnsHeldNpc_AndEmptiesWhenLast()
    {
        tracker.TryBeginEngagement(firstPlayer, Notable);

        var ended = tracker.TryEndEngagement(firstPlayer, out var npcKey);

        Assert.True(ended);
        Assert.Equal(Notable, npcKey);
        Assert.True(tracker.IsEmpty);
    }

    [Fact]
    public void TryEndEngagement_WhenNoEngagement_Fails()
    {
        var ended = tracker.TryEndEngagement(firstPlayer, out var npcKey);

        Assert.False(ended);
        Assert.Null(npcKey);
    }

    [Fact]
    public void IsEngagedByOther_WhenHeldBySamePlayer_IsFalse()
    {
        tracker.TryBeginEngagement(firstPlayer, Notable);

        Assert.False(tracker.IsEngagedByOther(Notable, firstPlayer));
    }

    [Fact]
    public void TwoPlayers_DifferentNpcs_BothEngage()
    {
        Assert.True(tracker.TryBeginEngagement(firstPlayer, Notable));
        Assert.True(tracker.TryBeginEngagement(secondPlayer, OtherNotable));

        Assert.False(tracker.IsEmpty);
        Assert.True(tracker.IsEngagedByOther(Notable, secondPlayer));
        Assert.True(tracker.IsEngagedByOther(OtherNotable, firstPlayer));
    }
}
