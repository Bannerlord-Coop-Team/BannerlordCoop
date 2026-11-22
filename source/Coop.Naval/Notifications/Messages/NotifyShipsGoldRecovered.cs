using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace Coop.Naval.Notifications.Messages;

public readonly struct NotifyShipsGoldRecovered : IEvent
{
    public readonly Clan Clan;
    public readonly Hero PartyOwner;
    public readonly int Gold;

    public NotifyShipsGoldRecovered(
        Clan clan,
        Hero partyOwner,
        int gold)
    {
        Clan = clan;
        PartyOwner = partyOwner;
        Gold = gold;
    }
}
