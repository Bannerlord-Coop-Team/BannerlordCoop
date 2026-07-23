using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Messages;

public readonly struct OnTroopWoundedAttempted : IEvent
{
    public readonly MapEventParty MapEventParty;
    public readonly int TroopSeed;

    public OnTroopWoundedAttempted(MapEventParty mapEventParty, int troopSeed)
    {
        MapEventParty = mapEventParty;
        TroopSeed = troopSeed;
    }
}
