using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MapEvent), nameof(MapEvent.GetMapEventSide))]
class DebugFixPatch2
{
    [HarmonyPrefix]
    static bool Prefix(MapEvent __instance, ref MapEventSide __result, BattleSideEnum side)
    {
        __result = __instance._sides[(int)side];

        return false;
    }
}
