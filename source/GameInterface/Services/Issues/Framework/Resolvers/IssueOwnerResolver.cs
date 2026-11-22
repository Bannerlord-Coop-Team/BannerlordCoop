using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

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

        return TryResolveOwnerPlayer(issue, out var player) &&
            objectManager.TryGetObject(player.HeroId, out ownerHero);
    }

    public bool TryResolveOwnerParty(IssueBase issue, out MobileParty ownerParty)
    {
        ownerParty = null;
        return TryResolveOwnerPlayer(issue, out var player) &&
            objectManager.TryGetObjectWithLogging(player.MobilePartyId, out ownerParty);
    }

    private bool TryResolveOwnerPlayer(IssueBase issue, out Player player)
    {
        player = null;
        return TryGetOwnerControllerId(issue, out var controllerId) &&
            playerManager.TryGetPlayer(controllerId, out player);
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
}
