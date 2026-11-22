using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueResolver
{
    // Fails when the hero has no issue, the issue is a different one than the id names, or its type is not registered
    bool TryResolve(
        string issueOwnerId,
        string issueId,
        out Hero issueOwner,
        out IssueBase issue,
        out IQuestTypeDescriptor descriptor);
}
