using GameInterface.Services.MapEvents;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

public class ConversationPartyTrackerTests
{
    private readonly ConversationPartyTracker tracker = new ConversationPartyTracker(objectManager: null);

    private readonly object firstPlayer = new object();
    private readonly object secondPlayer = new object();

    [Fact]
    public void TryBeginEngagement_WhenPartyUnengaged_BeginsEngagement()
    {
        var began = tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);

        Assert.True(began);
        Assert.True(tracker.TryGetEngagement("lord1", out var engagement));
        Assert.Equal(firstPlayer, engagement.EngagerKey);
        Assert.Equal("player1", engagement.EngagerPartyId);
        Assert.False(engagement.WasAiDisabled);
    }

    [Fact]
    public void TryBeginEngagement_WhenPartyEngagedByOtherPlayer_Fails()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);

        var began = tracker.TryBeginEngagement(secondPlayer, "player2", "lord1", wasAiDisabled: false);

        Assert.False(began);
        Assert.True(tracker.TryGetEngagement("lord1", out var engagement));
        Assert.Equal(firstPlayer, engagement.EngagerKey);
    }

    [Fact]
    public void TryBeginEngagement_WhileEngagedWithDifferentParty_Fails()
    {
        // First approval wins: a player's live engagement must not be superseded by a later request, whose
        // approval could otherwise release the first party while its conversation is still opening.
        tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);

        var began = tracker.TryBeginEngagement(firstPlayer, "player1", "lord2", wasAiDisabled: false);

        Assert.False(began);
        Assert.True(tracker.TryGetEngagement("lord1", out _));
        Assert.False(tracker.TryGetEngagement("lord2", out _));
    }

    [Fact]
    public void TryBeginEngagement_AfterEndingPreviousEngagement_Succeeds()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);
        tracker.TryEndEngagement(firstPlayer, out _, out _);

        var began = tracker.TryBeginEngagement(firstPlayer, "player1", "lord2", wasAiDisabled: false);

        Assert.True(began);
        Assert.False(tracker.TryGetEngagement("lord1", out _));
        Assert.True(tracker.TryGetEngagement("lord2", out _));
    }

    [Fact]
    public void TryBeginEngagement_WhenSamePlayerReengagesSameParty_KeepsOriginalAiDisabledState()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);

        // The refresh passes true because the party is disabled by the hold itself by now.
        var began = tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: true);

        Assert.True(began);
        Assert.True(tracker.TryGetEngagement("lord1", out var engagement));
        Assert.False(engagement.WasAiDisabled);
    }

    [Fact]
    public void TryEndEngagement_WhenEngaged_ReturnsHeldParty()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);

        var ended = tracker.TryEndEngagement(firstPlayer, out var partyId, out var engagement);

        Assert.True(ended);
        Assert.Equal("lord1", partyId);
        Assert.False(engagement.WasAiDisabled);
        Assert.False(tracker.TryGetEngagement("lord1", out _));
    }

    [Fact]
    public void TryEndEngagement_WhenNotEngaged_Fails()
    {
        Assert.False(tracker.TryEndEngagement(firstPlayer, out _, out _));
    }

    [Fact]
    public void TryEndEngagement_DoesNotAffectOtherPlayersEngagement()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);
        tracker.TryBeginEngagement(secondPlayer, "player2", "lord2", wasAiDisabled: false);

        tracker.TryEndEngagement(firstPlayer, out _, out _);

        Assert.False(tracker.TryGetEngagement("lord1", out _));
        Assert.True(tracker.TryGetEngagement("lord2", out _));
        Assert.False(tracker.IsEmpty);
    }

    [Fact]
    public void IsEngagedByOther_TrueOnlyForDifferentEngager()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);

        Assert.False(tracker.IsEngagedByOther("lord1", firstPlayer));
        Assert.True(tracker.IsEngagedByOther("lord1", secondPlayer));
        Assert.False(tracker.IsEngagedByOther("lord2", secondPlayer));
    }

    [Fact]
    public void IsEmpty_TracksEngagementLifecycle()
    {
        Assert.True(tracker.IsEmpty);

        tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);
        Assert.False(tracker.IsEmpty);

        tracker.TryEndEngagement(firstPlayer, out _, out _);
        Assert.True(tracker.IsEmpty);
    }

    [Fact]
    public void TryBeginEngagement_WithNullPartyOrEngager_Fails()
    {
        Assert.False(tracker.TryBeginEngagement(firstPlayer, "player1", null, wasAiDisabled: false));
        Assert.False(tracker.TryBeginEngagement(null, "player1", "lord1", wasAiDisabled: false));
        Assert.True(tracker.IsEmpty);
    }

    [Fact]
    public void Dispose_ReleasesAndClearsEngagements()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);

        // With no object manager the release is a no-op, but the state must still be fully cleared.
        tracker.Dispose();

        Assert.True(tracker.IsEmpty);
        Assert.False(tracker.TryGetEngagement("lord1", out _));
    }
}
