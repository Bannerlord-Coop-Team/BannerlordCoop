using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Common.Logging;
using Serilog;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

internal class MerchantOutlawsFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<MerchantOutlawsFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest merchantQuest) || proof != (byte)MerchantOutlawsBranch.Success)
        {
            return false;
        }

        if (merchantQuest.IsOngoing && merchantQuest._questPartyProgress >= merchantQuest._totalPartyCount)
        {
            merchantQuest.SuccessConsequences();
        }
        else
        {
            Logger.Warning("Skipping success for {quest}: ongoing={ongoing}, progress={progress}/{required}",
                merchantQuest.StringId, merchantQuest.IsOngoing, merchantQuest._questPartyProgress, merchantQuest._totalPartyCount);
        }

        return true;
    }
}
