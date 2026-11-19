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

    // Replaces TryEndEngagement_SharedParty_ReleasesOnlyAfterLastPlayer, which asserted that two
    // players could hold the same party at once. Sharing existed so simultaneous ATTACKERS converged
    // on one MapEvent, but every server-side conversation outcome is authorised only as "this peer
    // holds an engagement with this party", so a shared hold also let two players each apply the same
    // one-shot result - two recruiters persuading one lord, both paying, the lord defecting twice.
    // Attackers still converge: the holder starts the battle and the contender's retry joins that
    // MapEvent through the attackerInMapEvent/defenderInMapEvent branch in ConversationRequestHandler.
    [Fact]
    public void TryBeginEngagement_PartyHeldByAnotherPlayer_IsRejectedUntilReleased()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "lord1", wasAiDisabled: false);

        Assert.False(tracker.TryBeginEngagement(secondPlayer, "player2", "lord1", wasAiDisabled: true));
        Assert.False(tracker.IsEngagerParty("lord1", "player2"));
        Assert.True(tracker.IsEngagerParty("lord1", "player1"));

        // The holder releasing frees the party outright - there is no second holder to wait for.
        tracker.TryEndEngagement(firstPlayer, out _, out _, out var releaseAfterFirst);
        Assert.True(releaseAfterFirst);
        Assert.True(tracker.IsEmpty);

        Assert.True(tracker.TryBeginEngagement(secondPlayer, "player2", "lord1", wasAiDisabled: true));
        tracker.TryEndEngagement(secondPlayer, out _, out _, out var releaseAfterSecond);
        Assert.True(releaseAfterSecond);
        Assert.True(tracker.IsEmpty);
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

    [Fact]
    public void TryDefer_WithoutConversations_IsFalse()
    {
        Assert.False(tracker.TryDefer(firstPlayer, "player1", "looter1", out _));
    }

    [Fact]
    public void TryDefer_WhenEngagedWithSameParty_IsFalse()
    {
        // The same party attacking again refreshes the engagement, which the bandit barter needs.
        tracker.TryBeginEngagement(firstPlayer, "player1", "looter1", wasAiDisabled: false);

        Assert.False(tracker.TryDefer(firstPlayer, "player1", "looter1", out _));
    }

    [Fact]
    public void TryDefer_WhenEngagedWithDifferentParty_IsTrue()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "looter1", wasAiDisabled: false);

        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter2", out _));
    }

    [Fact]
    public void TryDefer_WhenPartyHeldByAnotherPlayer_IsTrue()
    {
        tracker.TryBeginEngagement(secondPlayer, "player2", "looter1", wasAiDisabled: false);

        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter1", out _));
    }

    [Fact]
    public void TryDefer_WhenPlayerInPvpConversation_IsTrue()
    {
        tracker.BeginPvpConversation("player1", "player2");

        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter1", out _));
    }

    [Fact]
    public void TryDefer_WhenAnotherPlayerIsBusy_IsFalse()
    {
        tracker.TryBeginEngagement(secondPlayer, "player2", "looter2", wasAiDisabled: false);
        tracker.BeginPvpConversation("player3", "player4");

        Assert.False(tracker.TryDefer(firstPlayer, "player1", "looter1", out _));
    }

    [Fact]
    public void TryDefer_AfterConversationsEnd_IsFalse()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "looter1", wasAiDisabled: false);
        tracker.BeginPvpConversation("player2", "player3");

        tracker.TryEndEngagement(firstPlayer, out _, out _);
        tracker.EndPvpConversation("player3");

        Assert.False(tracker.TryDefer(firstPlayer, "player1", "looter2", out _));
        Assert.False(tracker.TryDefer(secondPlayer, "player2", "looter1", out _));
    }

    [Fact]
    public void TryDefer_WithNullArgument_IsFalse()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "looter1", wasAiDisabled: false);
        tracker.BeginPvpConversation("player2", "player3");

        Assert.False(tracker.TryDefer(null, "player1", "looter2", out _));
        Assert.False(tracker.TryDefer(secondPlayer, null, "looter1", out _));
        Assert.False(tracker.TryDefer(firstPlayer, "player1", null, out _));
    }

    [Fact]
    public void TryDefer_WhileTheConversationLasts_IsFirstOncePerParty()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "looter1", wasAiDisabled: false);

        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter2", out var looter2First));
        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter3", out var looter3First));
        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter2", out var looter2Again));
        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter3", out var looter3Again));
        Assert.True(tracker.TryDefer(secondPlayer, "player2", "looter1", out var otherPlayerFirst));
        Assert.True(looter2First);
        Assert.True(looter3First);
        Assert.False(looter2Again);
        Assert.False(looter3Again);
        Assert.True(otherPlayerFirst);
    }

    [Fact]
    public void TryDefer_AfterThePlayersEngagementEnds_IsFirstAgain()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "looter1", wasAiDisabled: false);
        tracker.TryDefer(firstPlayer, "player1", "looter2", out _);

        tracker.TryEndEngagement(firstPlayer, out _, out _);
        tracker.TryBeginEngagement(firstPlayer, "player1", "looter3", wasAiDisabled: false);

        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter2", out var firstDeferral));
        Assert.True(firstDeferral);
    }

    [Fact]
    public void TryDefer_AfterAStaleEndIsRejected_IsNotFirstAgain()
    {
        tracker.TryBeginEngagement(firstPlayer, "player1", "looter1", wasAiDisabled: false, requestId: "current-request");
        tracker.TryDefer(firstPlayer, "player1", "looter2", out _);

        Assert.False(tracker.TryEndEngagement(
            firstPlayer,
            out _,
            out _,
            out _,
            expectedRequestId: "older-request",
            requireRequestIdMatch: true));

        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter2", out var firstDeferral));
        Assert.False(firstDeferral);
    }

    [Fact]
    public void TryDefer_AfterTheOtherHolderReleasesTheParty_IsFirstAgainForThatParty()
    {
        var thirdPlayer = new object();
        tracker.TryBeginEngagement(secondPlayer, "player2", "looter1", wasAiDisabled: false);
        tracker.TryBeginEngagement(thirdPlayer, "player3", "looter2", wasAiDisabled: false);
        tracker.TryDefer(firstPlayer, "player1", "looter1", out _);
        tracker.TryDefer(firstPlayer, "player1", "looter2", out _);

        // A new holder takes looter1 after the old one lets it go; looter2 is still held by the same player.
        tracker.TryEndEngagement(secondPlayer, out _, out _);
        tracker.TryBeginEngagement(new object(), "player4", "looter1", wasAiDisabled: false);

        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter1", out var looter1First));
        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter2", out var looter2First));
        Assert.True(looter1First);
        Assert.False(looter2First);
    }

    [Fact]
    public void TryDefer_AfterThePlayersPvpConversationEnds_IsFirstAgain()
    {
        tracker.BeginPvpConversation("player1", "player2");
        tracker.TryDefer(firstPlayer, "player1", "looter1", out _);

        tracker.EndPvpConversation("player2");
        tracker.BeginPvpConversation("player1", "player3");

        Assert.True(tracker.TryDefer(firstPlayer, "player1", "looter1", out var firstDeferral));
        Assert.True(firstDeferral);
    }
}
