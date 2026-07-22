using Common.Messaging;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>[Server] One active human left the battle, so a waiting player can claim the freed slot.</summary>
internal readonly struct BattleHumanSlotFreed : IEvent
{
    public readonly string MapEventId;
    public readonly string PartyId;
    public readonly int TroopSeed;

    public BattleHumanSlotFreed(string mapEventId, string partyId, int troopSeed)
    {
        MapEventId = mapEventId;
        PartyId = partyId;
        TroopSeed = troopSeed;
    }
}
