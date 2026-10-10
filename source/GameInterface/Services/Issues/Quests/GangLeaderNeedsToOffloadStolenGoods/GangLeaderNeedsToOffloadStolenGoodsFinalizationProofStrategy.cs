using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

internal class GangLeaderNeedsToOffloadStolenGoodsFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest gangLeaderQuest))
        {
            return false;
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
            case GangLeaderNeedsToOffloadStolenGoodsBranch.FailByLosingHideoutBattle:
                gangLeaderQuest.FailQuestByLosingHideoutBattle();
                return true;
            default:
                return false;
        }
    }
}
