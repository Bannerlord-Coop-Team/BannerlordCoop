using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages;

public readonly struct TrackTroopForUpgrades : IEvent
{
    public readonly string MapEventPartyId;
    public readonly string CharacterId;

    public TrackTroopForUpgrades(
        string mapEventPartyId,
        string characterId)
    {
        MapEventPartyId = mapEventPartyId;
        CharacterId = characterId;
    }
}
