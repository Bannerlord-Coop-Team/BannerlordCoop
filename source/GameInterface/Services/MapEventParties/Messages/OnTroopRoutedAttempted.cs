using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Messages;

internal readonly struct OnTroopRoutedAttempted : IEvent
{
    public readonly MapEventParty MapEventParty;
    public readonly int TroopSeed;

    public OnTroopRoutedAttempted(MapEventParty mapEventParty, int troopSeed)
    {
        MapEventParty = mapEventParty;
        TroopSeed = troopSeed;
    }
}
