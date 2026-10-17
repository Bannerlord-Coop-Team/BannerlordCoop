using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd.Patches;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
internal class HeadmanHerdIssueCancelPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, out IssueBase __state)
    {
        __state = null;
        if (!(__instance is Issue) || !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) return true;
        return dispatcher.BeforeIssueOutcome(__instance, IssueOutcome.QuestCancel, out __state);
    }

    [HarmonyPostfix]
    private static void Postfix(IssueBase __state)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.After(__state, IssueOutcome.QuestCancel);
    }
}
