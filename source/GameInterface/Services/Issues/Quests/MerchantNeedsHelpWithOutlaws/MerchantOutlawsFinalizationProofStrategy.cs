using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

internal class MerchantOutlawsFinalizationProofStrategy : IFinalizationProofStrategy
{
    internal const byte Success = 1;

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest merchantQuest) || proof != Success)
        {
            return false;
        }

        if (merchantQuest.IsOngoing && merchantQuest._questPartyProgress >= merchantQuest._totalPartyCount)
        {
            merchantQuest.SuccessConsequences();
        }

        return true;
    }
}
