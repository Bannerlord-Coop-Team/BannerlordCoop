using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons;

// The quest takes its amount, reward and bribe from the issue, which creation capture already made the same everywhere
internal class GangLeaderNeedsWeaponsQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
    }
}
