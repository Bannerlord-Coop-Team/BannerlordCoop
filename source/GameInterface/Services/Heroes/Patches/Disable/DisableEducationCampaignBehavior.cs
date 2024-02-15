using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(EducationCampaignBehavior))]
internal class DisableEducationCampaignBehavior
{
    [HarmonyPatch(nameof(EducationCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
