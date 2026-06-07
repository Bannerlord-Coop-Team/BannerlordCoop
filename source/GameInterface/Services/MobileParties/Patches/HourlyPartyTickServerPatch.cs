using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(CampaignEvents))]
internal class HourlyPartyTickServerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("HourlyTickParty")]
    private static bool HourlyTickPartyPrefix(ref MobileParty mobileParty)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }

}