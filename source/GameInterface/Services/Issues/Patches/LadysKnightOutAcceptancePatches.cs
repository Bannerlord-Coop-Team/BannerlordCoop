using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures this accept's authoritative <c>_difficultyMultiplier</c> - see
/// <see cref="ILadysKnightOutIssueInterface"/>'s doc comment. Own independent postfix.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class LadysKnightOutQuestAcceptancePatch
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not LadysKnightOutIssueBehavior.LadysKnightOutIssue) return;

        if (!ContainerProvider.TryResolve<ILadysKnightOutIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var difficultyMultiplier)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new LadysKnightOutIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId, difficultyMultiplier));
    }
}

/// <summary>
/// Bug-1-shaped fix: gates the QUEST's two tournament-outcome methods
/// (<c>PlayerReachedTournamentRoundGoal</c>/<c>PlayerCouldntReachedTournamentRoundGoal</c>) to the recorded
/// owner only. Both are invoked from <c>OnPlayerEliminatedFromTournament</c>/<c>OnTournamentFinished</c>, which
/// react to the LOCAL peer's own tournament participation/result - so a non-owner peer's own mirrored quest
/// object would otherwise also collect this issue's reward/relation change whenever THEY personally win (or
/// get eliminated from) any unrelated tournament of their own.
/// </summary>
[HarmonyPatch(typeof(LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest))]
internal class LadysKnightOutOwnershipGatePatches
{
    [HarmonyPatch("PlayerReachedTournamentRoundGoal")]
    [HarmonyPrefix]
    private static bool PlayerReachedTournamentRoundGoalPrefix(LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("PlayerCouldntReachedTournamentRoundGoal")]
    [HarmonyPrefix]
    private static bool PlayerCouldntReachedTournamentRoundGoalPrefix(LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}

/// <summary>
/// Disables the (currently entirely unsynced - see <see cref="ILadysKnightOutIssueInterface"/>'s doc comment)
/// Lord Solution path for this issue type by forcing its <c>IsThereLordSolution</c> getter to always report
/// false. Vanilla's own dialogue system already gates the whole "use your influence instead" option on this
/// property, so this alone is enough to hide it - no other change needed.
/// </summary>
[HarmonyPatch(typeof(LadysKnightOutIssueBehavior.LadysKnightOutIssue), "IsThereLordSolution", MethodType.Getter)]
internal class LadysKnightOutLordSolutionDisablePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}
