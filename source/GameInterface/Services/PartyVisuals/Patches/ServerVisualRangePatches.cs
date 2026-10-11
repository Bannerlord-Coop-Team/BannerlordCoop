using Common;
using HarmonyLib;
using SandBox.View.Map.Visuals;

namespace GameInterface.Services.PartyVisuals.Patches;

[HarmonyPatch(typeof(MobilePartyVisual), nameof(MobilePartyVisual.ValidateIsDirty))]
internal class ServerVisualRangePatches
{
    [HarmonyPrefix]
    private static bool ValidateIsDirtyPrefix(MobilePartyVisual __instance)
    {
        if (ModInformation.IsClient) return true;

        return !ContainerProvider.TryResolve<IServerVisualRange>(out var visualRange) ||
            visualRange.ShouldRefresh(__instance.MapEntity);
    }
}
