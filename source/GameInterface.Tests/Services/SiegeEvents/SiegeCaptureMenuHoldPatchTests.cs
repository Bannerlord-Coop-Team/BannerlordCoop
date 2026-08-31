using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

/// <summary>Verifies capture aftermath holds cover every fortification entry menu.</summary>
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
}
