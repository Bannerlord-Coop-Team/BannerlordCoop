using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueOwnerResolver
{
    // The hero of the player that owns the issue, fails when nobody owns it yet
    bool TryResolveOwnerHero(IssueBase issue, out Hero ownerHero);

    // The controller id of the player that owns the issue, fails when nobody is known to own it
    bool TryGetOwnerControllerId(IssueBase issue, out string controllerId);
}
