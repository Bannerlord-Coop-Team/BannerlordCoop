using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IQuestSolutionAcceptStrategy
{
    // Accepting machine: read anything the accept rolled that the quest type needs everywhere
    byte[] Capture(IssueBase issue);

    // Every mirror: force what Capture read
    void Apply(IssueBase issue, byte[] captured);
}

// Optional hook for issue types that need to undo more than the generic quest
// mirror cleanup when the server rejects an optimistic local acceptance.
public interface IQuestSolutionAcceptRejectionStrategy
{
    void Reject(IssueBase issue);
}
