using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueAcceptPreconditions
{
    // Whether vanilla lets the player take the issue, checked as that player
    bool CanTake(IssueBase issue, Hero playerHero, string controllerId);
}
