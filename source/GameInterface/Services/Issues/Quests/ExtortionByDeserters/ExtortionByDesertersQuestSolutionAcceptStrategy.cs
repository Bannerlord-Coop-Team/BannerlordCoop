using System;
using Common;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

/// <summary>The server's constructor creates the party, then binds the owner's quest to it.</summary>
internal class ExtortionByDesertersQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IExtortionByDesertersProgress progress;

    public ExtortionByDesertersQuestSolutionAcceptStrategy(IExtortionByDesertersProgress progress)
    {
        this.progress = progress;
    }

    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (ModInformation.IsServer && issue.IssueQuest is Quest quest)
        {
            progress.Send(quest);
        }
    }
}
