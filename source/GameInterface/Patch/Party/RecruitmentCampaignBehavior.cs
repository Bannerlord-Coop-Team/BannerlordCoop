using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Mod.Patch.Party
{
    [HarmonyPatch(typeof(RecruitmentCampaignBehavior), "CheckRecruiting")]
    internal class RecruitmentCampaignBehaviorPatch
    {
        private static bool Prefix(ref MobileParty mobileParty, ref Settlement settlement)
        {
            // TODO only allow for server and broadcast when it happens
            return true;
        }
    }
}