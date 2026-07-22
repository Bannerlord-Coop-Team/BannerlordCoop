using Common.Messaging;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>[Server] A campaign party is about to be removed from an active battle.</summary>
internal readonly struct BattlePartyLeaving : IEvent
{
    public readonly string MapEventId;
    public readonly string MapEventPartyId;

    public BattlePartyLeaving(string mapEventId, string mapEventPartyId)
    {
        MapEventId = mapEventId;
        MapEventPartyId = mapEventPartyId;
    }
}
