using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// IsPlayerMapEvent should be true on the server when the MapEvent includes at least one player party.
/// </summary>
[HarmonyPatch(typeof(MapEvent))]
internal class IsPlayerMapEventPatch
{
    [HarmonyPatch(nameof(MapEvent.IsPlayerMapEvent), MethodType.Getter)]
    [HarmonyPostfix]
    public static void IsPlayerMapEventPostfix(MapEvent __instance, ref bool __result)
    {
        if (ModInformation.IsClient) return;

        if (__instance.ContainsPlayerParty())
        {
            __result = true;
        }
    }
}
