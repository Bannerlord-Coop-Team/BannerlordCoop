using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>Creates and removes canonical dropped-weapon entities for network reconciliation.</summary>
public interface IWeaponDropWorldItemSpawner
{
    bool IsReady { get; }
    bool IsPresent(SpawnedItemEntity item);
    bool IsRemoved(SpawnedItemEntity item);
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
        out SpawnedItemEntity item,
        MissionObject parent = null);
    bool TryRemove(SpawnedItemEntity item);
}

/// <inheritdoc cref="IWeaponDropWorldItemSpawner"/>
public sealed class WeaponDropWorldItemSpawner : IWeaponDropWorldItemSpawner
{
    public bool IsReady =>
        Mission.Current != null &&
        Mission.Current.CurrentState == Mission.State.Continuing &&
        Mission.Current.Scene != null;

    public bool IsPresent(SpawnedItemEntity item) =>
        item != null &&
        !item.IsRemoved &&
        item.GameEntity.IsValid &&
        (!item.IsDeactivated ||
         (item.WeaponCopy.Item != null &&
          item.WeaponCopy.Item.ItemFlags.HasAnyFlag(ItemFlags.CannotBePickedUp)));

    public bool IsRemoved(SpawnedItemEntity item) =>
        item != null && item.IsRemoved;

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
        out SpawnedItemEntity item,
        MissionObject parent = null)
    {
        item = null;
        if (!IsReady || weapon.IsEmpty) return false;

        GameEntity entity;
        const Mission.WeaponSpawnFlags stuckFlags = Mission.WeaponSpawnFlags.AsMissile | Mission.WeaponSpawnFlags.WithStaticPhysics;
        if ((spawnFlags & stuckFlags) == stuckFlags)
        {
            entity = GameEntityExtensions.Instantiate(Mission.Current.Scene, weapon,
                spawnFlags.HasAnyFlag(Mission.WeaponSpawnFlags.WithHolster), needBatchedVersion: true);
            // Destructible objects parent stuck missiles to their current state rather than the root.
            parent?.AddStuckMissile(entity);
            MatrixFrame globalFrame = parent == null ? frame : parent.GameEntity.GetGlobalFrame().TransformToParent(in frame);
            entity.SetGlobalFrame(in globalFrame);
            entity.CreateAndAddScriptComponent(nameof(SpawnedItemEntity), callScriptCallbacks: true);
            Mission.Current.SpawnWeaponAux(entity.WeakEntity, weapon, spawnFlags, Vec3.Zero, Vec3.Zero, hasLifeTime);
        }
        else
        {
            entity = Mission.Current.SpawnWeaponWithNewEntityAux(
                weapon,
                spawnFlags,
                frame,
                forcedSpawnIndex: -1,
                attachedMissionObject: parent,
                hasLifeTime);
        }
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
