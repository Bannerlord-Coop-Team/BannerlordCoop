using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

internal static class WeaponDropItemTracker
{
    public static HashSet<SpawnedItemEntity> Capture()
    {
        if (Mission.Current == null) return new HashSet<SpawnedItemEntity>();
        return new HashSet<SpawnedItemEntity>(
            Mission.Current.MissionObjects.OfType<SpawnedItemEntity>());
    }

    public static SpawnedItemEntity FindDroppedItem(HashSet<SpawnedItemEntity> existingItems)
    {
        if (Mission.Current == null || existingItems == null) return null;
        return Mission.Current.MissionObjects
            .OfType<SpawnedItemEntity>()
            .FirstOrDefault(item => !existingItems.Contains(item));
    }
}
