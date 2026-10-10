using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods.Patches;

// Vanilla names this FailQuestByGivingBackTheGoods but it ends the quest with a betrayal
[HarmonyPatch(typeof(Quest), nameof(Quest.FailQuestByGivingBackTheGoods))]
internal class GangLeaderNeedsToOffloadStolenGoodsBetrayalByGivingGoodsBackPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeQuestBranch(__instance, (byte)GangLeaderNeedsToOffloadStolenGoodsBranch.BetrayalByGivingGoodsBack);
    }
}
