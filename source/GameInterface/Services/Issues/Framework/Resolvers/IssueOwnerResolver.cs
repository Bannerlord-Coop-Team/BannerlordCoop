using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Framework.Resolvers;

internal class IssueOwnerResolver : IIssueOwnerResolver
{
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;

    public IssueOwnerResolver(
        IObjectManager objectManager,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.ownership = ownership;
        this.playerManager = playerManager;
    }

    public bool TryResolveOwnerHero(IssueBase issue, out Hero ownerHero)
    {
        ownerHero = null;

        if (!TryGetOwnerControllerId(issue, out var controllerId))
        {
            return false;
        }

        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            return false;
        }

        return objectManager.TryGetObject(player.HeroId, out ownerHero);
    }

    public bool TryGetOwnerControllerId(IssueBase issue, out string controllerId)
    {
        controllerId = null;

        if (!objectManager.TryGetId(issue.IssueOwner, out var issueOwnerId))
        {
            return false;
        }

        return ownership.TryGetOwner(issueOwnerId, issue.StringId, out controllerId);
    }

    public bool TryGetOwnerControllerId(MBObjectBase[] relatedObjects, out string controllerId)
    {
        controllerId = null;

        var issue = relatedObjects.OfType<IssueBase>().FirstOrDefault()
            ?? relatedObjects.OfType<QuestBase>().Select(IssueManager.GetIssueOfQuest).FirstOrDefault(found => found != null);

        return issue != null && TryGetOwnerControllerId(issue, out controllerId);
    }
}
