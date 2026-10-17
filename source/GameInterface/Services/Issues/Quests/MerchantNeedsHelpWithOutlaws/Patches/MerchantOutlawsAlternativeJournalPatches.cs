using Common;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws.Patches;

[HarmonyPatch]
internal class MerchantOutlawsAlternativeJournalPatches
{
    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
    [HarmonyPostfix]
    private static void DailyPostfix(IssueManager __instance)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress)) return;
        foreach (var issue in __instance.Issues.Values.OfType<Issue>()) progress.Send(issue);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void FinalizedPrefix(IssueBase __instance)
    {
        if (__instance is Issue && ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress)) progress.Send(__instance);
    }
}
