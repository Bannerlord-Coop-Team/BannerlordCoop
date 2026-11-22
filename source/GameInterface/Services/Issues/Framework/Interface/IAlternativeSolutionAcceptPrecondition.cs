using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IAlternativeSolutionAcceptPrecondition
{
    bool CanAccept(IssueBase issue, Hero playerHero);
}
