using Common;
using HarmonyLib;
using Common.Messaging;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws.Patches;

[HarmonyPatch]
internal class MerchantOutlawsAlternativeJournalPatches
{
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.OnAlternativeSolutionSolvedAndTroopsAreReturning))]
    [HarmonyPrefix]
    private static bool ReturningPrefix(IssueBase __instance)
    {
        if (ModInformation.IsServer || !(__instance is Issue)) return true;

        // The server sends this log, so a client tick must not append it again.
        __instance._areIssueEffectsResolved = true;
        return false;
    }

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
        if (ModInformation.IsClient && __instance is Issue && !__instance.IsSolvingWithAlternative &&
            __instance.AlternativeSolutionSentTroops.TotalManCount > 0)
        {
            // A different solver can finish while this client's committed picker selection is pending.
            MessageBroker.Instance.Publish(__instance, new MerchantOutlawsAlternativeCancelled(__instance));
        }

        // Framework dismissal ends the issue directly, without finalizing its unstarted server quest.
        if (ModInformation.IsServer && __instance is Issue && __instance.IssueQuest is Quest quest && quest.IsOngoing)
        {
            quest.FinalizeQuest();
            quest.AfterFinalize();
        }

        if (__instance is Issue && ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress)) progress.Send(__instance);
    }
}
