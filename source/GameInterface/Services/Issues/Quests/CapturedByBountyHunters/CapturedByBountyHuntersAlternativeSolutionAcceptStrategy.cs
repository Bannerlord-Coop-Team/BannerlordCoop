using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters;

// The alternative solution only rolls what the framework already syncs
internal class CapturedByBountyHuntersAlternativeSolutionAcceptStrategy : IAlternativeSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
    }
}
