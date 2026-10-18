using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using Common;
using GameInterface.Policies;
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
        if (ModInformation.IsClient && __instance.IsSolvingWithAlternative && CallOriginalPolicy.IsOriginalAllowed())
        {
            __instance.IssueFinalized();
            return false;
        }
        return dispatcher.BeforeIssueOutcome(__instance, IssueOutcome.QuestCancel, out __state);
    }

    [HarmonyPostfix]
    private static void Postfix(IssueBase __state)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.After(__state, IssueOutcome.QuestCancel);
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class HeadmanHerdDismissalPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase __instance)
    {
        if (ModInformation.IsClient || !(__instance is Issue)) return;
        // Startup dismissal has no ownership registry, so the saved original party owns this cleanup.
        if (ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup)) cleanup.Finalize(__instance);
    }
}
