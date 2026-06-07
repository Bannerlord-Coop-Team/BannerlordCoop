using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(MerchantNeedsHelpWithOutlawsIssueQuestBehavior))]
internal class DisableMerchantNeedsHelpWithOutlawsIssueQuestBehavior
{
    [HarmonyPatch(nameof(MerchantNeedsHelpWithOutlawsIssueQuestBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
