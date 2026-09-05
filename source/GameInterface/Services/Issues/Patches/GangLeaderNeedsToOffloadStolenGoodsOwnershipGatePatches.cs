using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest))]
internal class GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches
{
    private static bool IsLocalPeerOwner(TaleWorlds.CampaignSystem.Hero questGiver) =>
        ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) && ownershipRegistry.IsLocalPeerOwner(questGiver);

    [HarmonyPatch("SucceedQuestByPayingAndKeepingTheGoods")]
    [HarmonyPrefix]
    private static bool SucceedQuestByPayingAndKeepingTheGoodsPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("SucceedQuestByPayingAndGivingTheGoodsBack")]
    [HarmonyPrefix]
    private static bool SucceedQuestByPayingAndGivingTheGoodsBackPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("FailQuestByKeepingTheGoods")]
    [HarmonyPrefix]
    private static bool FailQuestByKeepingTheGoodsPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("FailQuestByGivingBackTheGoods")]
    [HarmonyPrefix]
    private static bool FailQuestByGivingBackTheGoodsPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("FailQuestByLosingHideoutBattle")]
    [HarmonyPrefix]
    private static bool FailQuestByLosingHideoutBattlePrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnSettlementLeft")]
    [HarmonyPrefix]
    private static bool OnSettlementLeftPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnSettlementOwnerChanged")]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("GameMenuOpened")]
    [HarmonyPrefix]
    private static bool GameMenuOpenedPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("game_menu_approach_meeting_on_condition")]
    [HarmonyPrefix]
    private static bool GameMenuApproachMeetingOnConditionPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance,
        ref bool __result)
    {
        if (IsLocalPeerOwner(__instance.QuestGiver)) return true;

        __result = false;
        return false;
    }

    [HarmonyPatch("OnMapEventStarted")]
    [HarmonyPrefix]
    private static bool OnMapEventStartedPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnHideoutBattleCompleted")]
    [HarmonyPrefix]
    private static bool OnHideoutBattleCompletedPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        IsLocalPeerOwner(__instance.QuestGiver);
}

[HarmonyPatch]
internal class GangLeaderNeedsToOffloadStolenGoodsPriceFreezePatches
{
    [HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), "StolenTradeGoodAmount", MethodType.Getter)]
    [HarmonyPrefix]
    private static bool StolenTradeGoodAmountPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue __instance, ref int __result)
    {
        if (!GangLeaderNeedsToOffloadStolenGoodsQuestType.AlternativeSolutionFreeze.TryGetFrozen(__instance.IssueOwner, out var frozen)) return true;

        __result = frozen.StolenTradeGoodAmount;
        return false;
    }

    [HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), "RewardGold", MethodType.Getter)]
    [HarmonyPrefix]
    private static bool RewardGoldPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue __instance, ref int __result)
    {
        if (!GangLeaderNeedsToOffloadStolenGoodsQuestType.AlternativeSolutionFreeze.TryGetFrozen(__instance.IssueOwner, out var frozen)) return true;

        __result = frozen.RewardGold;
        return false;
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class GangLeaderNeedsToOffloadStolenGoodsFinalizeCleanupPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (__instance is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue) return;
        if (DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted(__instance) && !IssueFinalizeAuthorityGuard.IsActive) return;

        GangLeaderNeedsToOffloadStolenGoodsQuestType.AlternativeSolutionFreeze.Clear(__instance.IssueOwner);
    }
}
