using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

internal class GangLeaderNeedsToOffloadStolenGoodsFinalizationProofStrategy : IFinalizationProofStrategy
{
    private readonly GangLeaderBranchEvidence evidence;

    public GangLeaderNeedsToOffloadStolenGoodsFinalizationProofStrategy(GangLeaderBranchEvidence evidence)
    {
        this.evidence = evidence;
    }

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest gangLeaderQuest))
        {
            return false;
        }

        var issue = IssueManager.GetIssueOfQuest(gangLeaderQuest);
        if (issue == null || !evidence.Consume(issue, proof) || !MatchesVanillaState(gangLeaderQuest, proof))
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
            case GangLeaderNeedsToOffloadStolenGoodsBranch.FailByLosingHideoutBattle:
                gangLeaderQuest.FailQuestByLosingHideoutBattle();
                return true;
            default:
                return false;
        }
    }

    private static bool MatchesVanillaState(Quest quest, byte proof)
    {
        var branch = (GangLeaderNeedsToOffloadStolenGoodsBranch)proof;
        if (branch == GangLeaderNeedsToOffloadStolenGoodsBranch.FailByLosingHideoutBattle)
        {
            return quest._isFightingForGoods;
        }

        var isSuccess = branch is GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByKeepingGoods or
            GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByGivingGoodsBack;
        if (quest._isPayingForGoods != isSuccess)
        {
            return false;
        }

        var keepsGoods = branch is GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByKeepingGoods or
            GangLeaderNeedsToOffloadStolenGoodsBranch.BetrayalByKeepingGoods;
        return quest._playerHasTheGoods == keepsGoods;
    }

}
