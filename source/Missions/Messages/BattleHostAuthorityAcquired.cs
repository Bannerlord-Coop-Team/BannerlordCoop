using Common.Messaging;

namespace Missions.Messages;

/// <summary>[Client, local] This client received a new host epoch for a battle.</summary>
public readonly struct BattleHostAuthorityAcquired : IEvent
{
    public readonly string MapEventId;

    public BattleHostAuthorityAcquired(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
