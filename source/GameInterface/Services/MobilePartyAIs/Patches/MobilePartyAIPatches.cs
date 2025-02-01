using HarmonyLib;
using TaleWorlds.CampaignSystem;
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
            if (__instance._mobileParty == null) return false;
            return true;
        }

        [HarmonyPatch(nameof(MobilePartyAi.CheckPartyNeedsUpdate))]
        [HarmonyPrefix]
        static void Prefix(ref MobilePartyAi __instance)
        {
            if (ModInformation.IsServer) return;

            if (__instance._mobileParty != MobileParty.MainParty) return;

            EncounterManager.HandleEncounterForMobileParty(__instance._mobileParty, 0f);
        }
    }
}
