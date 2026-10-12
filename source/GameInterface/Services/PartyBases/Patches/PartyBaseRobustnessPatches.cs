using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.PartyBases.Patches;


[HarmonyPatch(typeof(PartyBase))]
internal class PartyBaseRobustnessPatches
{

    [HarmonyPatch(nameof(PartyBase.Ships), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Postfix(ref PartyBase __instance, ref MBReadOnlyList<Ship> __result)
    {
        if (__result is null)
        {
            __instance._ships = new MBList<Ship>();
            __result = __instance._ships;
        }
    }

    // Client created parties skip the field initializer, so the ship list starts null
    [HarmonyPatch(nameof(PartyBase.AddShipInternal))]
    [HarmonyPrefix]
    private static void Prefix_AddShipInternal(ref PartyBase __instance) => EnsureShips(__instance);

    [HarmonyPatch(nameof(PartyBase.RemoveShipInternal))]
    [HarmonyPrefix]
    private static void Prefix_RemoveShipInternal(ref PartyBase __instance) => EnsureShips(__instance);

    private static void EnsureShips(PartyBase __instance)
    {
        if (__instance._ships is null)
        {
            __instance._ships = new MBList<Ship>();
        }
    }
}
