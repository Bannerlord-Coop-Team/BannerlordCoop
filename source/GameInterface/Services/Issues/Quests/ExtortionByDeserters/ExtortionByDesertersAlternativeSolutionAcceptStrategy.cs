using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

/// <summary>The framework captures companion risk, casualties and return times.</summary>
internal class ExtortionByDesertersAlternativeSolutionAcceptStrategy : IAlternativeSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue) => System.Array.Empty<byte>();
    public void Apply(IssueBase issue, byte[] captured) { }
}
