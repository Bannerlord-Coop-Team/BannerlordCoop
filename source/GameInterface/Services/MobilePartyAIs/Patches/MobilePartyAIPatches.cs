using GameInterface.Extentions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches
{
    [HarmonyPatch(typeof(MobilePartyAi))]
    internal class MobilePartyAIPatches
    {
        [HarmonyPatch("GetTargetPositionAndFace")]
        [HarmonyPrefix]
        static bool GetTargetPositionAndFace_Fix(ref MobilePartyAi __instance)
        {
            // Maybe fixes crashing on server for null ref exception
            if (__instance.GetMobileParty() == null) return false;
            return true;
        }
    }
}
