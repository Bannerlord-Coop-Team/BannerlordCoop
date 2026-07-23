using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

//[HarmonyPatch(typeof(MobilePartyAi))]
class MobilePartyAIRobustnessPatches
{
    [HarmonyPatch(nameof(MobilePartyAi.GetNearbyPartyToFlee))]
    [HarmonyPrefix]
    // Skipe if partyToFleeFrom is null
    private static bool RobustnessPrefix_GetNearbyPartyToFlee(MobileParty partyToFleeFrom) => partyToFleeFrom != null;
}
