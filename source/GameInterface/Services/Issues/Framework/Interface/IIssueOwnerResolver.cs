using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueOwnerResolver
{
    // The hero of the player that owns the issue, fails when nobody owns it yet
    bool TryResolveOwnerHero(IssueBase issue, out Hero ownerHero);
}
