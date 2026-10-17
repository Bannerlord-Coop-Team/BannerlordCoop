using Common.Messaging;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles.Message;

/// <summary>Local notification that a stuck missile on this client became a pickable world item.</summary>
public readonly struct MissileItemDropped : IEvent
{
    public int MissileIndex { get; }
    public Agent Shooter { get; }
    public SpawnedItemEntity Item { get; }

    public MissileItemDropped(int missileIndex, Agent shooter, SpawnedItemEntity item)
    {
        MissileIndex = missileIndex;
        Shooter = shooter;
        Item = item;
    }
}

/// <summary>Local notification that a stuck missile item was named by the world-item id its shot shares on every client.</summary>
public readonly struct MissileItemIdentified : IEvent
{
    public SpawnedItemEntity Item { get; }
    public Guid WorldItemId { get; }

    public MissileItemIdentified(SpawnedItemEntity item, Guid worldItemId)
    {
        Item = item;
        WorldItemId = worldItemId;
    }
}
