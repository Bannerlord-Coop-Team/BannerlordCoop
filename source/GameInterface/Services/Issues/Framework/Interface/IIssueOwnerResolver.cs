using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueOwnerResolver
{
    // The hero of the player that owns the issue, fails when nobody owns it yet
    bool TryResolveOwnerHero(IssueBase issue, out Hero ownerHero);

    // The registered party of the player who owns the issue, including while the hero is captive.
    // Fails if the issue has no owning player or the player's party cannot be resolved.
    bool TryResolveOwnerParty(IssueBase issue, out MobileParty ownerParty);

    // The controller id of the player that owns the issue, fails when nobody is known to own it
    bool TryGetOwnerControllerId(IssueBase issue, out string controllerId);

    bool IsLocalOwner(IssueBase issue);
}
