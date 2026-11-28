using Common.Util;
using GameInterface.Services.MapEvents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class BattleJoinRequestTrackerTests
{
    private readonly BattleJoinRequestTracker tracker = new BattleJoinRequestTracker();
    private readonly PartyBase party = ObjectHelper.SkipConstructor<PartyBase>();
    private readonly MapEvent joinedEvent = ObjectHelper.SkipConstructor<MapEvent>();
    private readonly MapEvent otherEvent = ObjectHelper.SkipConstructor<MapEvent>();

    [Fact]
    public void TryCancel_OtherMapEvent_LeavesTheJoinUncancelled()
    {
        Assert.True(tracker.TryStart(party, joinedEvent, "request"));
        Assert.False(tracker.TryCancel(party, otherEvent));

        Assert.True(tracker.TryComplete(party, "request", out var cancelled));
        Assert.False(cancelled);
    }

    [Fact]
    public void IsCancelled_MatchesOnlyTheCancelledMapEvent()
    {
        Assert.True(tracker.TryStart(party, joinedEvent, "request"));
        Assert.True(tracker.TryCancel(party, joinedEvent));

        Assert.True(tracker.IsCancelled(party, joinedEvent));
        Assert.False(tracker.IsCancelled(party, otherEvent));
    }

    // A removal can land before the reply, which still needs the cancel to send its leave.
    [Fact]
    public void EndCancelled_BeforeTheReply_KeepsTheCancel()
    {
        Assert.True(tracker.TryStart(party, joinedEvent, "request"));
        Assert.True(tracker.TryCancel(party, joinedEvent));
        tracker.EndCancelled(party);

        Assert.True(tracker.TryComplete(party, "request", out var cancelled));
        Assert.True(cancelled);
    }

    // A second Leave can land between the accepted reply and the removal, which must still end the cancel.
    [Fact]
    public void TryCancel_AfterTheCancelledReply_KeepsTheCancelEndable()
    {
        Assert.True(tracker.TryStart(party, joinedEvent, "request"));
        Assert.True(tracker.TryCancel(party, joinedEvent));
        Assert.True(tracker.TryComplete(party, "request", out var cancelled));
        Assert.True(cancelled);

        Assert.True(tracker.TryCancel(party, joinedEvent));
        tracker.EndCancelled(party);

        Assert.True(tracker.TryStart(party, joinedEvent, "next request"));
    }
}
