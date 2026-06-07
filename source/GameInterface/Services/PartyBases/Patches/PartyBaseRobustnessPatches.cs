using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using GameInterface.Policies;

namespace GameInterface.Services.PartyBases.Patches;


[HarmonyPatch(typeof(PartyBase))]
internal class PartyBaseRobustnessPatches
{

    [HarmonyPatch(nameof(PartyBase.Ships), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Postfix(ref PartyBase __instance, ref MBReadOnlyList<Ship> __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (__result is null)
        {
            __instance._ships = new MBList<Ship>();
            __result = __instance._ships;
        }
    }
}
