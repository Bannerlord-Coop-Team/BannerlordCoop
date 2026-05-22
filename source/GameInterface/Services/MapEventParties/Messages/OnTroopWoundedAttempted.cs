using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Messages;

public readonly struct OnTroopWoundedAttempted : IEvent
{
    public readonly MapEventParty MapEventParty;
    public readonly CharacterObject Troop;

    public OnTroopWoundedAttempted(MapEventParty mapEventParty, CharacterObject troop)
    {
        MapEventParty = mapEventParty;
        Troop = troop;
    }
}
