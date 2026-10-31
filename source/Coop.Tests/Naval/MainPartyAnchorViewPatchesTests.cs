#if COOP_NAVAL
using Common.Util;
using Coop.Naval.Anchors.Patches;
using Coop.Tests.Server.Services.MobileParties;
using HarmonyLib;
using NavalDLC.GauntletUI.Map;
using NavalDLC.View.Map.Managers;
using NavalDLC.ViewModelCollection.Map;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.Naval;

/// <summary>
/// Covers the naval map views when the main party has no anchor, like the server's removed main party.
/// </summary>
[Collection(nameof(CampaignCurrentCollection))]
public class MainPartyAnchorViewPatchesTests
{
    // Both patch classes must bind to the installed game's view methods, otherwise the server map NREs every frame.
    [Theory]
    [InlineData(typeof(AnchorVisualManagerPatches))]
    [InlineData(typeof(NavalMapAnchorTrackerViewPatches))]
    public void Patches_BindToTheInstalledViews(Type patchClass)
    {
        var harmony = new Harmony($"coop.tests.naval.{patchClass.Name}");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(patchClass).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // The anchor visual manager ticks without throwing when the main party has no anchor.
    [Fact]
    public void AnchorVisualManagerTick_WithoutMainPartyAnchor_DoesNotThrow()
    {
        var manager = ObjectHelper.SkipConstructor<AnchorVisualManager>();

        var exception = RunWithAnchorlessMainParty(
            nameof(AnchorVisualManager.OnTick),
            typeof(AnchorVisualManager),
            typeof(AnchorVisualManagerPatches),
            "OnTickPrefix",
            () => manager.OnTick(0f, 0f));

        Assert.Null(exception);
    }

    // Anchor position updates, which OnInitialize shares the check with, treat a missing anchor as no visual.
    [Fact]
    public void AnchorVisualManagerPositionUpdate_WithoutMainPartyAnchor_DoesNotThrow()
    {
        var manager = ObjectHelper.SkipConstructor<AnchorVisualManager>();

        var exception = RunWithAnchorlessMainParty(
            nameof(AnchorVisualManager.CanPartyHaveAnchor),
            typeof(AnchorVisualManager),
            typeof(AnchorVisualManagerPatches),
            "CanPartyHaveAnchorPrefix",
            () => manager.OnAnchorPositionUpdated());

        Assert.Null(exception);
    }

    // The fleet tracker hides itself instead of reading the missing anchor's position.
    [Fact]
    public void AnchorTrackerUpdate_WithoutMainPartyAnchor_HidesTheTracker()
    {
        var view = ObjectHelper.SkipConstructor<GauntletNavalMapAnchorTrackerView>();
        view._dataSource = ObjectHelper.SkipConstructor<MapAnchorTrackerVM>();
        view._dataSource._isVisible = true;

        var exception = RunWithAnchorlessMainParty(
            nameof(GauntletNavalMapAnchorTrackerView.OnMapScreenUpdate),
            typeof(GauntletNavalMapAnchorTrackerView),
            typeof(NavalMapAnchorTrackerViewPatches),
            "OnMapScreenUpdatePrefix",
            () => view.OnMapScreenUpdate(0f));

        Assert.Null(exception);
        Assert.False(view._dataSource.IsVisible);
    }

    // Runs the vanilla call with only the given prefix applied and a current campaign whose active main party has no anchor.
    private static Exception RunWithAnchorlessMainParty(string method, Type target, Type patchClass, string prefix, Action act)
    {
        var previousCampaign = Campaign.Current;
        var harmony = new Harmony($"coop.tests.naval.anchorless.{prefix}");
        try
        {
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            campaign.MainParty = ObjectHelper.SkipConstructor<MobileParty>();
            campaign.MainParty.IsActive = true;
            Campaign.Current = campaign;

            harmony.Patch(AccessTools.Method(target, method), prefix: new HarmonyMethod(AccessTools.Method(patchClass, prefix)));
            return Record.Exception(act);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Campaign.Current = previousCampaign;
        }
    }
}
#endif
