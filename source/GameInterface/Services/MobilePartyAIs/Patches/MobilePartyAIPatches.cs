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
            // Default path on server
            if (ModInformation.IsServer) return;

            if (__instance._mobileParty != MobileParty.MainParty)
            {
                // Disable all parties that are not the player
                __instance.DefaultBehaviorNeedsUpdate = false;
                return;
            }

            __instance.DefaultBehaviorNeedsUpdate = true;
        }
    }
}
