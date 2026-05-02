using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Messages;

public readonly struct OnTroopKilledAttempted : IEvent
{
    public readonly MapEventParty MapEventParty;
    public readonly int TroopSeed;

    public OnTroopKilledAttempted(MapEventParty mapEventParty, int troop)
    {
        MapEventParty = mapEventParty;
        TroopSeed = troop;
    }
}
