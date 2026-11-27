using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueDismisser
{
    // Ends the issue without running any vanilla outcome, freeing companions it sent as troops
    void Dismiss(IssueBase issue);

    // Drops sent companions whose death already ran without making them Dead, the way vanilla's troop return drops dead ones
    void LeaveBehindDeadCompanions(IssueBase issue);

    // Whether it still has to be taken out of the manager
    bool IsInManager(IssueBase issue);
}
