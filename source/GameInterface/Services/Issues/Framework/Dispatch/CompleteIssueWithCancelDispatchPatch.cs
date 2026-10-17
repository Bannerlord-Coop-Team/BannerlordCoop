using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
internal class CompleteIssueWithCancelDispatchPatch
{
    [HarmonyPrefix]
    private static bool Before(IssueBase __instance, out IssueBase __state)
    {
        __state = null;
        // Quest cancellation already announces its own outcome.
        return __instance.IssueQuest != null || !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeIssueOutcome(__instance, IssueOutcome.QuestCancel, out __state);
    }

    [HarmonyPostfix]
    private static void After(IssueBase __state)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.After(__state, IssueOutcome.QuestCancel);
    }
}
