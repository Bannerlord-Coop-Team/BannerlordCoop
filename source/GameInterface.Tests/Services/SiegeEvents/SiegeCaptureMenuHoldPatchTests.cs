using Common.Util;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

/// <summary>Verifies capture aftermath holds cover every fortification entry menu and the stale encounter menu.</summary>
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
    public void TryGetHeldSettlement_LiveEncounter_MatchesOnlyItsOwnSettlement()
    {
        var held = ObjectHelper.SkipConstructor<Settlement>();
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        SiegeCaptureMenuHoldPatch.HoldFor(held);
        try
        {
            encounter.EncounterSettlementAux = ObjectHelper.SkipConstructor<Settlement>();
            Assert.False(SiegeCaptureMenuHoldPatch.TryGetHeldSettlement(encounter, out _));

            encounter.EncounterSettlementAux = held;
            Assert.True(SiegeCaptureMenuHoldPatch.TryGetHeldSettlement(encounter, out var settlement));
            Assert.Same(held, settlement);
        }
        finally
        {
            SiegeCaptureMenuHoldPatch.Release(held);
        }
    }

    [Fact]
    public void TryGetHeldSettlement_FinishedEncounter_UsesTheOnlyHeldCapture()
    {
        var first = ObjectHelper.SkipConstructor<Settlement>();
        var second = ObjectHelper.SkipConstructor<Settlement>();
        Assert.False(SiegeCaptureMenuHoldPatch.TryGetHeldSettlement(null, out _));

        SiegeCaptureMenuHoldPatch.HoldFor(first);
        try
        {
            Assert.True(SiegeCaptureMenuHoldPatch.TryGetHeldSettlement(null, out var settlement));
            Assert.Same(first, settlement);

            SiegeCaptureMenuHoldPatch.HoldFor(second);
            Assert.False(SiegeCaptureMenuHoldPatch.TryGetHeldSettlement(null, out _));
        }
        finally
        {
            SiegeCaptureMenuHoldPatch.Release(first);
            SiegeCaptureMenuHoldPatch.Release(second);
        }
    }
}
