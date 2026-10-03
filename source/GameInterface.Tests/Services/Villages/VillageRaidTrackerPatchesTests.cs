using System;
using GameInterface.Services.Villages.Patches;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using Xunit;

namespace GameInterface.Tests.Services.Villages;

[Collection(ModInformationRoleCollection.Name)]
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

    [Fact]
    public void RepeatedRaidRefreshDoesNotRestoreAPlayerClearedBookmark()
    {
        var state = new VillageRaidTrackerPatches.RaidBookmarkState();
        Assert.True(state.TryStartRaid());
        Assert.False(state.TryStartRaid());
    }

    [Fact]
    public void NewRaidAfterRaidEndCanTrackAgainWithoutOldBookmarkOwnership()
    {
        var state = new VillageRaidTrackerPatches.RaidBookmarkState();
        Assert.True(state.TryStartRaid());
        state.AutoTracked = true;
        state.EndRaid();
        Assert.False(state.AutoTracked);
        Assert.True(state.TryStartRaid());
    }

    [Fact]
    public void RaidTrackerBindsToNativeNameplateConstructor()
    {
        var harmony = new Harmony(nameof(VillageRaidTrackerPatchesTests));
        try
        {
            harmony.CreateClassProcessor(typeof(VillageRaidTrackerPatches)).Patch();
            var constructor = AccessTools.DeclaredConstructor(typeof(SettlementNameplateVM),
                new[] { typeof(Settlement), typeof(GameEntity), typeof(Camera), typeof(Action<CampaignVec2>) });
            Assert.NotNull(constructor);
            Assert.Contains(Harmony.GetPatchInfo(constructor).Postfixes,
                patch => patch.owner == harmony.Id && patch.PatchMethod.Name == nameof(VillageRaidTrackerPatches.NameplateCreated));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
