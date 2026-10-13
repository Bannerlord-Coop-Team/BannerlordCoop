using HarmonyLib;
using SandBox.View.Map.Visuals;

namespace GameInterface.Services.PartyVisuals.Patches;

[HarmonyPatch(typeof(MobilePartyVisual), nameof(MobilePartyVisual.ValidateIsDirty))]
internal class MapVisualRangePatches
{
    [HarmonyPrefix]
    private static bool ValidateIsDirtyPrefix(MobilePartyVisual __instance)
    {
        return !ContainerProvider.TryResolve<IMapVisualRange>(out var visualRange) ||
            visualRange.ShouldRefresh(__instance.MapEntity);
    }
}
