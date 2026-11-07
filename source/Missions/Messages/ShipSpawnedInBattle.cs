using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>[Game thread] A naval hull was spawned into the current mission on <see cref="Formation"/>.</summary>
public readonly struct ShipSpawnedInBattle : IEvent
{
    public readonly MissionObject Hull;
    public readonly Formation Formation;

    public ShipSpawnedInBattle(MissionObject hull, Formation formation)
    {
        Hull = hull;
        Formation = formation;
    }
}
