using HarmonyLib;
using NavalDLC.GauntletUI.Map;
using TaleWorlds.CampaignSystem;

namespace Coop.Naval.Anchors.Patches;

/// <summary>
/// Hides the zoomed out fleet tracker when the main party has no anchor, like the server's removed main party.
/// </summary>
[HarmonyPatch(typeof(GauntletNavalMapAnchorTrackerView))]
internal class NavalMapAnchorTrackerViewPatches
{
    // Vanilla reads MainParty.Anchor.Position before its own null check, so hide the tracker and skip the update
    [HarmonyPatch(nameof(GauntletNavalMapAnchorTrackerView.OnMapScreenUpdate))]
    [HarmonyPrefix]
    private static bool OnMapScreenUpdatePrefix(GauntletNavalMapAnchorTrackerView __instance)
    {
        if (Campaign.Current?.MainParty?.Anchor != null) return true;

        if (__instance._dataSource != null) __instance._dataSource.IsVisible = false;
        return false;
    }
}
