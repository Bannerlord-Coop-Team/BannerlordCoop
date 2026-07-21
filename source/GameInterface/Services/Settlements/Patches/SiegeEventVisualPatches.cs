using Common;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Refreshes client settlement presentation when its synced SiegeEvent reference changes. Vanilla dirties the
/// map visual and raises the nameplate events only in server-side siege lifecycle paths, so the client setter
/// apply must recreate those presentation side effects.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class SiegeEventVisualPatches
{
    [HarmonyPatch(nameof(Settlement.SiegeEvent), MethodType.Setter)]
    [HarmonyPrefix]
    private static void SetSiegeEventPrefix(Settlement __instance, out SiegeEvent __state)
    {
        __state = __instance.SiegeEvent;
    }

    [HarmonyPatch(nameof(Settlement.SiegeEvent), MethodType.Setter)]
    [HarmonyPostfix]
    private static void SetSiegeEventPostfix(Settlement __instance, SiegeEvent value, SiegeEvent __state)
    {
        __instance.Party?.SetLevelMaskIsDirty();
        __instance.Party?.SetVisualAsDirty();

        if (ModInformation.IsServer || __state == value) return;

        var nameplate = MapScreen.Instance?
            .GetMapView<GauntletMapSettlementNameplateView>()?
            ._dataSource?
            .GetNameplateOfSettlement(__instance);

        if (value == null)
        {
            nameplate?.OnSiegeEventEndedOnSettlement(__state);
        }
        else
        {
            nameplate?.OnSiegeEventStartedOnSettlement(value);
        }
    }
}
