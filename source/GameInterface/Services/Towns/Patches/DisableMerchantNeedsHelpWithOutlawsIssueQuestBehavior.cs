using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(MerchantNeedsHelpWithOutlawsIssueQuestBehavior))]
internal class DisableMerchantNeedsHelpWithOutlawsIssueQuestBehavior
{
    [HarmonyPatch(nameof(MerchantNeedsHelpWithOutlawsIssueQuestBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
