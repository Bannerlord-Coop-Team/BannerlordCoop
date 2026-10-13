using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ships.Messages;

public readonly struct LootShipsDiscardRequested : IEvent
{
    public readonly MobileParty Party;
    public readonly List<Ship> LootShips;

    public LootShipsDiscardRequested(MobileParty party, List<Ship> lootShips)
    {
        Party = party;
        LootShips = lootShips;
    }
}
