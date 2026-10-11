using Common;
using GameInterface;
using GameInterface.Services.PartyVisuals;
using HarmonyLib;
using NavalDLC.View.Map.Visuals;

namespace Coop.Naval.PartyVisuals.Patches;

[HarmonyPatch(typeof(NavalMobilePartyVisual), nameof(NavalMobilePartyVisual.ValidateIsDirty))]
internal class NavalServerVisualRangePatches
{
    [HarmonyPrefix]
    private static bool ValidateIsDirtyPrefix(NavalMobilePartyVisual __instance)
    {
        if (ModInformation.IsClient) return true;

        return !ContainerProvider.TryResolve<IServerVisualRange>(out var visualRange) ||
            visualRange.ShouldRefresh(__instance.MapEntity);
    }
}
