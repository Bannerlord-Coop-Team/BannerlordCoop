using Common.Util;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

/// <summary>Verifies capture aftermath holds cover every fortification entry menu and carry the defeated owner.</summary>
public class SiegeCaptureMenuHoldPatchTests
{
    [Theory]
    [InlineData(nameof(EncounterGameMenuBehavior.game_menu_town_outside_on_init))]
    [InlineData(nameof(EncounterGameMenuBehavior.game_menu_castle_outside_on_init))]
    public void CaptureHold_PatchesEveryFortificationOutsideMenu(string methodName)
    {
        var patchedMethods = typeof(SiegeCaptureMenuHoldPatch)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .SelectMany(method => method.GetCustomAttributes<HarmonyPatch>())
            .Where(patch => patch.info.declaringType == typeof(EncounterGameMenuBehavior))
            .Select(patch => patch.info.methodName);

        Assert.Contains(methodName, patchedMethods);
    }

    [Fact]
    public void HoldFor_KeepsTheHeldPreviousOwnerWhenReheldWithoutOne()
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var defeatedClan = ObjectHelper.SkipConstructor<Clan>();
        Assert.Null(SiegeCaptureMenuHoldPatch.GetHeldPreviousOwner(settlement));

        SiegeCaptureMenuHoldPatch.HoldFor(settlement, defeatedClan);
        try
        {
            // the encounter-menu backstop re-holds without knowing the owner
            SiegeCaptureMenuHoldPatch.HoldFor(settlement);

            Assert.True(SiegeCaptureMenuHoldPatch.IsHeld(settlement));
            Assert.Same(defeatedClan, SiegeCaptureMenuHoldPatch.GetHeldPreviousOwner(settlement));
        }
        finally
        {
            SiegeCaptureMenuHoldPatch.Release(settlement);
        }

        Assert.False(SiegeCaptureMenuHoldPatch.IsHeld(settlement));
        Assert.Null(SiegeCaptureMenuHoldPatch.GetHeldPreviousOwner(settlement));
    }

    [Fact]
    public void TryGetOnlyHeldSettlement_UsesTheHeldCaptureOnlyWhenUnambiguous()
    {
        var first = ObjectHelper.SkipConstructor<Settlement>();
        var second = ObjectHelper.SkipConstructor<Settlement>();
        Assert.False(SiegeCaptureMenuHoldPatch.TryGetOnlyHeldSettlement(out _));

        SiegeCaptureMenuHoldPatch.HoldFor(first);
        try
        {
            Assert.True(SiegeCaptureMenuHoldPatch.TryGetOnlyHeldSettlement(out var settlement));
            Assert.Same(first, settlement);

            SiegeCaptureMenuHoldPatch.HoldFor(second);
            Assert.False(SiegeCaptureMenuHoldPatch.TryGetOnlyHeldSettlement(out _));
        }
        finally
        {
            SiegeCaptureMenuHoldPatch.Release(first);
            SiegeCaptureMenuHoldPatch.Release(second);
        }
    }
}
