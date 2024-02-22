using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(PrisonerReleaseCampaignBehavior))]
internal class DisablePrisonerReleaseCampaignBehavior
{
    [HarmonyPatch(nameof(PrisonerReleaseCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
