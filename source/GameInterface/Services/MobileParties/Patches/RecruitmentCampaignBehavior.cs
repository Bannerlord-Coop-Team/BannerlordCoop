using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(RecruitmentCampaignBehavior), "CheckRecruiting")]
internal class RecruitmentCampaignBehaviorPatch
{
    private static bool Prefix(ref MobileParty mobileParty, ref Settlement settlement)
    {
        // TODO only allow for server and broadcast when it happens
        return true;
    }
}