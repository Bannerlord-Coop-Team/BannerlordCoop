using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons;

// The alternative solution has fixed math (no randomness)
internal class GangLeaderNeedsWeaponsAlternativeSolutionAcceptStrategy : IAlternativeSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
    }
}
