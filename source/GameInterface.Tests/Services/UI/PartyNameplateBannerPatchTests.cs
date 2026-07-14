using GameInterface.Services.UI.Patches;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class PartyNameplateBannerPatchTests
{
    [Fact]
    public void PartyBannerCodeTracker_KingdomBannerReplacesClanBanner_ReturnsTrue()
    {
        var tracker = new PartyBannerCodeTracker();

        Assert.False(tracker.Update("clan-banner"));
        Assert.True(tracker.Update("kingdom-banner"));
    }

    [Fact]
    public void PartyBannerCodeTracker_UnchangedBanner_ReturnsFalse()
    {
        var tracker = new PartyBannerCodeTracker();

        tracker.Update("kingdom-banner");

        Assert.False(tracker.Update("kingdom-banner"));
    }
}
