using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatch]
internal static class LesserNobleRevoltBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestSuccessPlayerComesToAnAgreementWithLesserNobleParty))]
    [HarmonyPrefix]
    private static bool Agreement(Quest __instance) => Before(__instance, LesserNobleRevoltBranch.SuccessByAgreement);

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestSuccessWithPlayerDefeatedLesserNobleParty))]
    [HarmonyPrefix]
    private static bool BattleWon(Quest __instance) => Before(__instance, LesserNobleRevoltBranch.SuccessByBattle);

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestFailWithPlayerDefeatedAgainstLesserNobleParty))]
    [HarmonyPrefix]
    private static bool BattleLost(Quest __instance) => Before(__instance, LesserNobleRevoltBranch.FailureByBattle);

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestFailedAfterTalkingWithLesserNobleParty))]
    [HarmonyPrefix]
    private static bool Released(Quest __instance) => Before(__instance, LesserNobleRevoltBranch.FailureByRelease);

    private static bool Before(Quest quest, LesserNobleRevoltBranch branch)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(quest, (byte)branch))
        {
            return true;
        }

        MapEventHelper.OnConversationEnd();
        return false;
    }
}
