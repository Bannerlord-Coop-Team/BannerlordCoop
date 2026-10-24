using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

internal class GangLeaderNeedsToOffloadStolenGoodsFinalizationProofStrategy : IFinalizationProofStrategy
{
    private readonly IGangLeaderGoodsService goods;

    public GangLeaderNeedsToOffloadStolenGoodsFinalizationProofStrategy(IGangLeaderGoodsService goods)
    {
        this.goods = goods;
    }

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest gangLeaderQuest))
        {
            return false;
        }

        var issue = IssueManager.GetIssueOfQuest(gangLeaderQuest);
        if (issue == null || !goods.TryConsume(gangLeaderQuest, proof))
        {
            return true;
        }

        switch ((GangLeaderNeedsToOffloadStolenGoodsBranch)proof)
        {
            case GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByKeepingGoods:
                gangLeaderQuest.SucceedQuestByPayingAndKeepingTheGoods();
                return true;
            case GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByGivingGoodsBack:
                gangLeaderQuest.SucceedQuestByPayingAndGivingTheGoodsBack();
                return true;
            case GangLeaderNeedsToOffloadStolenGoodsBranch.BetrayalByKeepingGoods:
                gangLeaderQuest.FailQuestByKeepingTheGoods();
                return true;
            case GangLeaderNeedsToOffloadStolenGoodsBranch.BetrayalByGivingGoodsBack:
                gangLeaderQuest.FailQuestByGivingBackTheGoods();
                return true;
            default:
                return false;
        }
    }

}
