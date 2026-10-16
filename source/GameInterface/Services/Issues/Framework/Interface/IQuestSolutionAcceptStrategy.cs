using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IQuestSolutionAcceptStrategy
{
    // Accepting machine: read anything the accept rolled that the quest type needs everywhere
    byte[] Capture(IssueBase issue);

    // Every mirror: force what Capture read
    void Apply(IssueBase issue, byte[] captured);
}
