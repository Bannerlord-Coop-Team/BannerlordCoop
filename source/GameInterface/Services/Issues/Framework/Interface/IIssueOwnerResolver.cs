using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueOwnerResolver
{
    // The hero of the player that owns the issue, fails when nobody owns it yet
    bool TryResolveOwnerHero(IssueBase issue, out Hero ownerHero);

    // The controller id of the player that owns the issue, fails when nobody is known to own it
    bool TryGetOwnerControllerId(IssueBase issue, out string controllerId);

    // The controller id of the owner of the first issue found in the objects, or of the issue a quest among them belongs to
    bool TryGetOwnerControllerId(MBObjectBase[] relatedObjects, out string controllerId);
}
