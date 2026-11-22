using Common;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

internal class MerchantOutlawsQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IMerchantOutlawsProgress progress;

    public MerchantOutlawsQuestSolutionAcceptStrategy(IMerchantOutlawsProgress progress)
    {
        this.progress = progress;
    }

    // Counts and rewards use only the difficulty already carried by the framework.
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (ModInformation.IsServer && issue.IssueQuest is Quest quest)
        {
            progress.Send(quest);
        }
    }
}
