using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.CraftingService.Patches;

[HarmonyPatch(typeof(CampaignEvents))]
internal class HourlyPartyTickServerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("HourlyTickParty")]
    private static bool HourlyTickPartyPrefix(ref MobileParty mobileParty)
    {
        if (ModInformation.IsServer)
        {
            if (mobileParty.IsPartyControlled())
            {
                return true;
            }
            return false;
        }
        return false;
    }

}