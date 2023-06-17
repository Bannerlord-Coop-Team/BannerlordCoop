using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;


[HarmonyPatch(typeof(RecruitPrisonersCampaignBehavior))]
internal class DisableRecruitPrisonersCampaignBehavior
{
    [HarmonyPatch(nameof(RecruitPrisonersCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
