using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.Dispatch;

internal class IssueOwnerParty : IIssueOwnerParty
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IIssueOwnerResolver owners;

    public IssueOwnerParty(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IIssueOwnerResolver owners)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.owners = owners;
    }

    public bool TryGetOwnerParty(IssueBase issue, out MobileParty party)
    {
        party = null;
        if (!owners.TryGetOwnerControllerId(issue, out var controller) ||
            !playerManager.TryGetPlayer(controller, out var player) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var ownerParty))
        {
            return false;
        }

        party = ownerParty;
        return true;
    }
}
