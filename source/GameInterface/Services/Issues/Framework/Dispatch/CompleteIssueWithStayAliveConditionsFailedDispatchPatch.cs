using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithStayAliveConditionsFailed))]
internal class CompleteIssueWithStayAliveConditionsFailedDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, out IssueBase __state)
    {
        __state = null;
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeIssueOutcome(__instance, IssueOutcome.IssueStayAliveFailed, out __state);
    }

    [HarmonyPostfix]
    private static void Postfix(IssueBase __state)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.After(__state, IssueOutcome.IssueStayAliveFailed);
    }
}
