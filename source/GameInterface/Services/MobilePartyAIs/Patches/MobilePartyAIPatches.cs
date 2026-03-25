using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches
{
    [HarmonyPatch(typeof(MobilePartyAi))]
    internal class MobilePartyAIPatches
    {
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
