using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;


[HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
internal class DisableRecruitmentCampaignBehavior
{
    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    static bool PrefixRegisterEvents()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.CheckRecruiting))]
    [HarmonyPrefix]
    /// Only allow recruiting for AI parties
    static bool PrefixCheckRecruiting(MobileParty mobileParty)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return !mobileParty.IsPlayerParty();
    }
}
