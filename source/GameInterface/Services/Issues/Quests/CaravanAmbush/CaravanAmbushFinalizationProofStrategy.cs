using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal class CaravanAmbushFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest caravan) || proof != (byte)CaravanAmbushBranch.CollectReward) return false;
        if (!caravan.IsOngoing || !caravan._isCaravanSaved || caravan._caravanParty == null || MobileParty.MainParty == null)
        {
            return false;
        }
        caravan.OnQuestSucceeded();
        return true;
    }
}
