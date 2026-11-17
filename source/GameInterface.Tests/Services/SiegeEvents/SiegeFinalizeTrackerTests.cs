using Common.Util;
using GameInterface.Services.SiegeEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

/// <summary>
/// Covers the finalize guard and the emptied camp snapshot the siege end patches keep per session.
/// </summary>
public class SiegeFinalizeTrackerTests
{
    private readonly SiegeFinalizeTracker tracker = new SiegeFinalizeTracker();
    private readonly SiegeEvent siegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();

    [Fact]
    public void TryBeginFinalize_AlreadyFinalizing_ReturnsFalseUntilEnded()
    {
        Assert.True(tracker.TryBeginFinalize(siegeEvent));
        Assert.True(tracker.IsFinalizing(siegeEvent));
        Assert.False(tracker.TryBeginFinalize(siegeEvent));

        tracker.EndFinalize(siegeEvent);

        Assert.False(tracker.IsFinalizing(siegeEvent));
        Assert.True(tracker.TryBeginFinalize(siegeEvent));
    }

    [Fact]
    public void IsFinalizing_OtherSiege_ReturnsFalse()
    {
        tracker.TryBeginFinalize(siegeEvent);

        Assert.False(tracker.IsFinalizing(ObjectHelper.SkipConstructor<SiegeEvent>()));
    }

    [Fact]
    public void TakeCampParties_ReturnsRecordedPartiesOnce()
    {
        var parties = new[] { ObjectHelper.SkipConstructor<MobileParty>() };
        tracker.RecordCampParties(siegeEvent, parties);

        Assert.Same(parties, tracker.TakeCampParties(siegeEvent));
        Assert.Null(tracker.TakeCampParties(siegeEvent));
    }

    [Fact]
    public void RecordCampParties_AgainForSameSiege_ReplacesSnapshot()
    {
        tracker.RecordCampParties(siegeEvent, new[] { ObjectHelper.SkipConstructor<MobileParty>() });
        var latest = new[] { ObjectHelper.SkipConstructor<MobileParty>() };

        tracker.RecordCampParties(siegeEvent, latest);

        Assert.Same(latest, tracker.TakeCampParties(siegeEvent));
    }

    [Fact]
    public void TakeCampParties_OtherSiege_ReturnsNull()
    {
        tracker.RecordCampParties(siegeEvent, new[] { ObjectHelper.SkipConstructor<MobileParty>() });

        Assert.Null(tracker.TakeCampParties(ObjectHelper.SkipConstructor<SiegeEvent>()));
    }
}
