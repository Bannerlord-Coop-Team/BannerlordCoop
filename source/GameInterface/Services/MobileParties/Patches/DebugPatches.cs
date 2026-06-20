using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
class DebugPatches
{
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.Position), MethodType.Setter)]
    [HarmonyPostfix]
    static void Postfix_IsLastSpeedCacheInvalid(MobileParty __instance)
    {
        if (__instance.IsPlayerParty() || __instance == MobileParty.MainParty)
        {
            ;
        }
    }
}
