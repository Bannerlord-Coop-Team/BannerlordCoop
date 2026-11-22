using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers;

// The framework carries the failure chance and casualties, nothing else is rolled
internal class ArmyOfPoachersAlternativeSolutionAcceptStrategy : IAlternativeSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
    }
}
