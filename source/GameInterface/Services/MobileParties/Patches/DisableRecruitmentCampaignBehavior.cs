using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;


[HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
internal class DisableRecruitmentCampaignBehavior
{
    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
