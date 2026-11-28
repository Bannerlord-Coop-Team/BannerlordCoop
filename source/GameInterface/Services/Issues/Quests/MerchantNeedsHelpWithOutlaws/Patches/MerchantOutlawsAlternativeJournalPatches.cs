using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class MerchantOutlawsAlternativeJournalPatches
{
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void FinalizedPrefix(IssueBase __instance)
    {
        // Framework dismissal ends the issue directly, without finalizing its unstarted server quest.
        if (ModInformation.IsServer && __instance is Issue && __instance.IssueQuest is Quest quest && quest.IsOngoing)
        {
            quest.FinalizeQuest();
            quest.AfterFinalize();
        }

        if (__instance is Issue && ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress)) progress.Send(__instance);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPostfix]
    private static void FinalizedPostfix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance is Issue && ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress))
            progress.Forget(__instance);
    }

}
