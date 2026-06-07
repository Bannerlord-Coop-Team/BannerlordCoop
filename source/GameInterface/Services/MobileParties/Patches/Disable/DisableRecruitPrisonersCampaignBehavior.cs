using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;


[HarmonyPatch(typeof(RecruitPrisonersCampaignBehavior))]
internal class DisableRecruitPrisonersCampaignBehavior
{
    [HarmonyPatch(nameof(RecruitPrisonersCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
