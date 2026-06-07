using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(PrisonerReleaseCampaignBehavior))]
internal class DisablePrisonerReleaseCampaignBehavior
{
    [HarmonyPatch(nameof(PrisonerReleaseCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
