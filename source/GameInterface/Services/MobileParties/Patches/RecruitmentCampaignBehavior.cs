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
    private static bool CheckRecruitingPrefix(ref MobileParty mobileParty, ref Settlement settlement) => ModInformation.IsServer;
}