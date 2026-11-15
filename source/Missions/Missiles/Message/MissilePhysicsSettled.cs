using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles.Message;

public readonly struct MissilePhysicsSettled : IEvent
{
    public SpawnedItemEntity Item { get; }

    public MissilePhysicsSettled(SpawnedItemEntity item)
    {
        Item = item;
    }
}
