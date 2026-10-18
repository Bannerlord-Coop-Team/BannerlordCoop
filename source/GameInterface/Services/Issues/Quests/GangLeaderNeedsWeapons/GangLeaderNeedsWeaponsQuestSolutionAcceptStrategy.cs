using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons;

// The quest takes its amount, reward and bribe from the issue's weapon class and price (made the same everywhere by
// creation capture) and from the difficulty the accept already forwards to the server
internal class GangLeaderNeedsWeaponsQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
    }
}
