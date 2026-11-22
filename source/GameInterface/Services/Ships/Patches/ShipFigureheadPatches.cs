using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Ships.Patches;

/// <summary>
/// Invalidates a ship's cached version when its figurehead is set by AutoSync.
/// </summary>
[HarmonyPatch(typeof(Ship))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class ShipFigureheadPatches
{
    /// <summary>
    /// Dirties the version because the synced setter skips ChangeFigurehead, which normally does it.
    /// </summary>
    [HarmonyPatch(nameof(Ship.Figurehead), MethodType.Setter)]
    [HarmonyPostfix]
    private static void Postfix_SetFigurehead(Ship __instance) => __instance.UpdateVersionNo();
}
