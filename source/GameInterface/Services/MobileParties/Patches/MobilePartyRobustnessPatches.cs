using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class MobilePartyRobustnessPatches
{
    [HarmonyPatch(nameof(MobileParty.Anchor), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Postfix(ref MobileParty __instance, ref AnchorPoint __result)
    {
        if (__result is null)
        {
            var anchor = new AnchorPoint(__instance);
            __instance.Anchor = anchor;
            __result = anchor;
        }
    }
}
