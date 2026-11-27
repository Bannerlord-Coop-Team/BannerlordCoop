using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The server's continuity pass pulled a player's party out of its own siege because it is no longer at war
/// with <see cref="Faction"/>, the case vanilla shows its "no longer enemies" menu for.
/// </summary>
public readonly struct PartyLeftSiegeByDiplomacy : IEvent
{
    public readonly MobileParty Party;
    public readonly IFaction Faction;

    public PartyLeftSiegeByDiplomacy(MobileParty party, IFaction faction)
    {
        Party = party;
        Faction = faction;
    }
}
