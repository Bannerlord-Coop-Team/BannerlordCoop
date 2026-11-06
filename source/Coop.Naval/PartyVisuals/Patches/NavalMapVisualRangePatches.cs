using GameInterface;
using GameInterface.Services.PartyVisuals;
using HarmonyLib;
using NavalDLC.View.Map.Visuals;

namespace Coop.Naval.PartyVisuals.Patches;

[HarmonyPatch(typeof(NavalMobilePartyVisual), nameof(NavalMobilePartyVisual.ValidateIsDirty))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class NavalMapVisualRangePatches
{
    [HarmonyPrefix]
    private static bool ValidateIsDirtyPrefix(NavalMobilePartyVisual __instance)
    {
        return !ContainerProvider.TryResolve<IMapVisualRange>(out var visualRange) ||
            visualRange.ShouldRefresh(__instance.MapEntity);
    }
}
