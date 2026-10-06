using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Registry;

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

        if (!objectManager.TryGetId(issue.IssueOwner, out var issueOwnerId))
        {
            return false;
        }

        if (!ownership.TryGetOwner(issueOwnerId, issue.StringId, out var controllerId))
        {
            return false;
        }

        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            return false;
        }

        return objectManager.TryGetObject(player.HeroId, out ownerHero);
    }
}
