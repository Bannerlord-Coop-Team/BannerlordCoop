using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

internal interface IEscortOwnerParty
{
    bool TryGet(IssueBase issue, out MobileParty party);
}

internal sealed class EscortOwnerParty : IEscortOwnerParty
{
    private readonly IIssueOwnerResolver owners;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;

    public EscortOwnerParty(IIssueOwnerResolver owners, IPlayerManager players, IObjectManager objects)
    {
        this.owners = owners;
        this.players = players;
        this.objects = objects;
    }

    public bool TryGet(IssueBase issue, out MobileParty party)
    {
        party = null;
        return owners.TryGetOwnerControllerId(issue, out var controller) && players.TryGetPlayer(controller, out var player) &&
            objects.TryGetObjectWithLogging(player.MobilePartyId, out party);
    }
}
