using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(PrisonerCaptureCampaignBehavior))]
internal class DisablePrisonerCaptureCampaignBehavior
{
    [HarmonyPatch(nameof(PrisonerCaptureCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
