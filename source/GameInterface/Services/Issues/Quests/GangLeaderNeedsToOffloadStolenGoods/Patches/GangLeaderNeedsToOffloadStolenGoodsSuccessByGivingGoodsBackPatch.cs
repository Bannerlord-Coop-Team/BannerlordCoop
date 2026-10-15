using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods.Patches;

[HarmonyPatch(typeof(Quest), nameof(Quest.SucceedQuestByPayingAndGivingTheGoodsBack))]
internal class GangLeaderNeedsToOffloadStolenGoodsSuccessByGivingGoodsBackPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeQuestBranch(__instance, (byte)GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByGivingGoodsBack);
    }
}
