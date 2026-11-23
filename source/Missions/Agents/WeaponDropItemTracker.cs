using System;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

/// <summary>Hands the world item a native agent drop spawned back to the caller that started the drop on this thread.</summary>
internal static class WeaponDropItemTracker
{
    [ThreadStatic]
    private static bool capturing;

    [ThreadStatic]
    private static SpawnedItemEntity droppedItem;

    // Whether a caller on this thread is waiting for the next native drop's world item.
    public static bool IsCapturing => capturing;

    // Starts waiting for the world item the next native agent drop on this thread spawns.
    public static void Begin()
    {
        capturing = true;
        droppedItem = null;
    }

    // Keeps the first world item a native drop spawned while a caller is waiting.
    public static void Record(SpawnedItemEntity item)
    {
        if (!capturing || droppedItem != null) return;

        droppedItem = item;
    }

    // Stops waiting and returns the recorded world item, or null when the drop spawned none.
    public static SpawnedItemEntity End()
    {
        SpawnedItemEntity item = droppedItem;
        capturing = false;
        droppedItem = null;
        return item;
    }

    // Runs a drop and returns the world item its native callback spawned.
    public static SpawnedItemEntity CaptureDroppedItem(Action drop)
    {
        SpawnedItemEntity item;
        Begin();
        try
        {
            drop();
        }
        finally
        {
            item = End();
        }

        return item;
    }

    // The native callback adds the world item script to the weapon entity it drops, so read it straight off it.
    public static SpawnedItemEntity FindDroppedItem(WeakGameEntity weaponEntity) =>
        weaponEntity.IsValid ? weaponEntity.GetFirstScriptOfType<SpawnedItemEntity>() : null;
}
