using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueOwnerParty
{
    bool TryGetOwnerParty(IssueBase issue, out MobileParty party);
}
