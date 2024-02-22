using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(PrisonerRecruitCampaignBehavior))]
internal class DisablePrisonerRecruitCampaignBehavior
{
    [HarmonyPatch(nameof(PrisonerRecruitCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
