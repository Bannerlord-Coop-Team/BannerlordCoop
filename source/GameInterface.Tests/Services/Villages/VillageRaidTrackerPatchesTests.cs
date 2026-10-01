using GameInterface.Services.Villages.Patches;
using Xunit;

namespace GameInterface.Tests.Services.Villages;

public class VillageRaidTrackerPatchesTests
{
    [Theory]
    [InlineData(0, true, false, true)]
    [InlineData(0, true, true, true)]
    [InlineData(0, false, false, false)]
    [InlineData(1, true, true, true)]
    [InlineData(1, true, false, false)]
    [InlineData(1, false, true, false)]
    [InlineData(2, true, true, false)]
    [InlineData(2, true, false, false)]
    public void RaidBookmarkHonorsPlayerFactionAndAutoTrackPreference(
        int autoTrack, bool sameFaction, bool isFactionLeader, bool expected)
    {
        Assert.Equal(expected,
            VillageRaidTrackerPatches.ShouldAutoTrackRaid(autoTrack, sameFaction, isFactionLeader));
    }

    [Theory]
    [InlineData(true, false, 0, false, true)]
    [InlineData(true, false, 1, false, true)]
    [InlineData(false, false, 0, false, false)]
    [InlineData(true, true, 0, false, false)]
    [InlineData(true, false, 2, false, false)]
    [InlineData(true, false, 0, true, false)]
    public void RaidEndPreservesUnrelatedManualDisabledAndStillAttackedBookmarks(
        bool autoTracked, bool manuallyTracked, int autoTrack, bool activeAttack, bool expected)
    {
        Assert.Equal(expected,
            VillageRaidTrackerPatches.ShouldRemoveRaidBookmark(autoTracked, manuallyTracked, autoTrack, activeAttack));
    }
}
