using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyPrisonerSold : IEvent
{
    public readonly PartyBase SellerParty;
    public readonly PartyBase BuyerParty;
    public readonly TroopRoster Prisoners;

    public NotifyPrisonerSold(PartyBase sellerParty, PartyBase buyerParty, TroopRoster prisoners)
    {
        SellerParty = sellerParty;
        BuyerParty = buyerParty;
        Prisoners = prisoners;
    }
}
