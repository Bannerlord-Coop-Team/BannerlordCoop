using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IAlternativeSolutionAcceptStrategy
{
    // Server: read anything quest specific the accept rolled
    byte[] Capture(IssueBase issue);

    // Every other machine: force what Capture read
    void Apply(IssueBase issue, byte[] captured);
}
