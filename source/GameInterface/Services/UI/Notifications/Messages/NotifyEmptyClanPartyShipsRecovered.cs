using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyEmptyClanPartyShipsRecovered : IEvent
{
    public readonly Hero PlayerHero;
    public readonly Hero PartyLeader;
    public readonly int Gold;

    public NotifyEmptyClanPartyShipsRecovered(Hero playerHero, Hero partyLeader, int gold)
    {
        PlayerHero = playerHero;
        PartyLeader = partyLeader;
        Gold = gold;
    }
}
