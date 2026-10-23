using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
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
    private static bool Released(Quest __instance)
    {
        if (Before(__instance, LesserNobleRevoltBranch.FailureByRelease))
        {
            return true;
        }

        // Vanilla leaves the revolt party's encounter first. The server's replay has no encounter to
        // leave, so the owner's client leaves its own.
        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.Finish();
        }

        return false;
    }

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
