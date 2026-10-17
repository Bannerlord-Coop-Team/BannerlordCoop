using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut.Patches;

[HarmonyPatch]
internal class LadysKnightOutBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.PlayerReachedTournamentRoundGoal))]
    [HarmonyPrefix]
    private static bool SuccessPrefix(Quest __instance) => BeforeBranch(__instance, LadysKnightOutBranch.TournamentSuccess);

    [HarmonyPatch(typeof(Quest), nameof(Quest.PlayerCouldntReachedTournamentRoundGoal))]
    [HarmonyPrefix]
    private static bool FailurePrefix(Quest __instance) => BeforeBranch(__instance, LadysKnightOutBranch.TournamentFailure);

    private static bool BeforeBranch(Quest quest, LadysKnightOutBranch branch)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }
}
