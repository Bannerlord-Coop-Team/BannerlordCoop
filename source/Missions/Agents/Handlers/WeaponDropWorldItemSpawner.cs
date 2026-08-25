using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>Creates and removes canonical dropped-weapon entities for network reconciliation.</summary>
public interface IWeaponDropWorldItemSpawner
{
    bool IsPresent(SpawnedItemEntity item);
    bool TryGetState(
        SpawnedItemEntity item,
        out MatrixFrame frame,
        out float remainingLifeTime);
    bool TrySpawn(
        ref MissionWeapon weapon,
        Mission.WeaponSpawnFlags spawnFlags,
        bool hasLifeTime,
        float remainingLifeTime,
        MatrixFrame frame,
        out SpawnedItemEntity item);
    bool TryRemove(SpawnedItemEntity item);
}

/// <inheritdoc cref="IWeaponDropWorldItemSpawner"/>
public sealed class WeaponDropWorldItemSpawner : IWeaponDropWorldItemSpawner
{
    public bool IsPresent(SpawnedItemEntity item) =>
        item != null &&
        !item.IsRemoved &&
        item.GameEntity.IsValid &&
        (!item.IsDeactivated ||
         (item.WeaponCopy.Item != null &&
          item.WeaponCopy.Item.ItemFlags.HasAnyFlag(ItemFlags.CannotBePickedUp)));

    public bool TryGetState(
        SpawnedItemEntity item,
        out MatrixFrame frame,
        out float remainingLifeTime)
    {
        frame = default;
        remainingLifeTime = 0f;
        if (!IsPresent(item)) return false;

        frame = item.GameEntity.GetGlobalFrame();
        if (item.HasLifeTime)
        {
            remainingLifeTime = MathF.Max(
                0f,
                item._deletionTimer.Duration -
                (Mission.Current.CurrentTime - item._deletionTimer.StartTime));
        }
        return true;
    }

    public bool TrySpawn(
        ref MissionWeapon weapon,
        Mission.WeaponSpawnFlags spawnFlags,
        bool hasLifeTime,
        float remainingLifeTime,
        MatrixFrame frame,
        out SpawnedItemEntity item)
    {
        item = null;
        if (Mission.Current == null || weapon.IsEmpty) return false;

        GameEntity entity = Mission.Current.SpawnWeaponWithNewEntityAux(
            weapon,
            spawnFlags,
            frame,
            forcedSpawnIndex: -1,
            attachedMissionObject: null,
            hasLifeTime);
        item = entity?.GetFirstScriptOfType<SpawnedItemEntity>();
        if (item == null)
        {
            entity?.Remove(0);
            return false;
        }

        if (hasLifeTime)
            item._deletionTimer.Reset(Mission.Current.CurrentTime, remainingLifeTime);
        return true;
    }

    public bool TryRemove(SpawnedItemEntity item)
    {
        if (!IsPresent(item)) return true;

        item.GameEntity.Remove(0);
        return !IsPresent(item);
    }
}
