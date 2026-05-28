using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Messages;

public readonly struct OnTroopKilledAttempted : IEvent
{
    public readonly MapEventParty MapEventParty;
    public readonly CharacterObject Troop;

    public OnTroopKilledAttempted(MapEventParty mapEventParty, CharacterObject troop)
    {
        MapEventParty = mapEventParty;
        Troop = troop;
    }
}
