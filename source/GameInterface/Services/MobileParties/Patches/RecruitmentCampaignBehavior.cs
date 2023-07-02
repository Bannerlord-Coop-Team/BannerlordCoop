using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
internal class RecruitmentCampaignBehaviorPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("CheckRecruiting")]
    private static bool CheckRecruitingPrefix(ref MobileParty mobileParty, ref Settlement settlement)
    {
        // TODO only allow for server and broadcast when it happens
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("HourlyTickParty")]
    private static bool HourlyTickPartyPrefix(ref MobileParty mobileParty)
    {
        // TODO disable for player parties
        return false;
    }
}