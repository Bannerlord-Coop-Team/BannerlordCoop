using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueDismisser
{
    // Ends the issue without running any vanilla outcome, freeing companions it sent as troops
    void Dismiss(IssueBase issue);
}
