using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(MerchantNeedsHelpWithOutlawsIssueQuestBehavior))]
internal class DisableMerchantNeedsHelpWithOutlawsIssueQuestBehavior
{
    [HarmonyPatch(nameof(MerchantNeedsHelpWithOutlawsIssueQuestBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
