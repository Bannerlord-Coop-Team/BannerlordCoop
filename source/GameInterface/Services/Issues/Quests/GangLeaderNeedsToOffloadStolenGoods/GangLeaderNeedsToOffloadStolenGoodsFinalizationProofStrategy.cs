using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

internal class GangLeaderNeedsToOffloadStolenGoodsFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderNeedsToOffloadStolenGoodsFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest gangLeaderQuest))
        {
            return false;
        }

        switch ((GangLeaderNeedsToOffloadStolenGoodsBranch)proof)
        {
            case GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByKeepingGoods:
                if (!CanResolveHandIn(gangLeaderQuest) || !gangLeaderQuest._isPayingForGoods) return Reject(gangLeaderQuest, proof);
                gangLeaderQuest.SucceedQuestByPayingAndKeepingTheGoods();
                return true;
            case GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByGivingGoodsBack:
                if (!CanResolveHandIn(gangLeaderQuest) || !gangLeaderQuest._isPayingForGoods) return Reject(gangLeaderQuest, proof);
                gangLeaderQuest.SucceedQuestByPayingAndGivingTheGoodsBack();
                return true;
            case GangLeaderNeedsToOffloadStolenGoodsBranch.BetrayalByKeepingGoods:
                if (!CanResolveHandIn(gangLeaderQuest) || gangLeaderQuest._isPayingForGoods) return Reject(gangLeaderQuest, proof);
                gangLeaderQuest.FailQuestByKeepingTheGoods();
                return true;
            case GangLeaderNeedsToOffloadStolenGoodsBranch.BetrayalByGivingGoodsBack:
                if (!CanResolveHandIn(gangLeaderQuest) || gangLeaderQuest._isPayingForGoods) return Reject(gangLeaderQuest, proof);
                gangLeaderQuest.FailQuestByGivingBackTheGoods();
                return true;
            case GangLeaderNeedsToOffloadStolenGoodsBranch.FailByLosingHideoutBattle:
                if (!gangLeaderQuest._isFightingForGoods) return Reject(gangLeaderQuest, proof);
                gangLeaderQuest.FailQuestByLosingHideoutBattle();
                return true;
            default:
                return false;
        }
    }

    private static bool CanResolveHandIn(Quest quest) =>
        quest._questHideout != null &&
        MobileParty.MainParty.CurrentSettlement == quest._questHideout &&
        quest._playerHasTheGoods;

    private static bool Reject(Quest quest, byte proof)
    {
        Logger.Warning("Rejected stolen-goods branch {proof} for {quest}: vanilla hand-in state is not satisfied", proof, quest.StringId);
        return true;
    }
}
