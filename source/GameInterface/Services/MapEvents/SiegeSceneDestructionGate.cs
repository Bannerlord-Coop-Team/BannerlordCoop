using System;

using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Deterministic seed for the vanilla siege scene's random wall dressing. Vanilla
/// <c>SiegeMissionPreparationHandler.ArrangeDestructedMeshes</c> pre-destroys a RANDOM subset of the
/// destructible wall pieces (chunks, merlons, rubble) and reveals random damage decals via
/// <c>MBRandom</c> = <c>Game.Current.RandomGenerator</c>, whose state differs per machine and never
/// replicates in coop (no GameNetwork session). So each client breaks different wall pieces at scene
/// build. The launcher binds a seed derived from the map-event id (identical on every client) to the
/// newly-created mission. <c>SiegeDestructionSeedPatch</c> consumes it later, when asynchronous mission
/// loading actually reaches <c>ArrangeDestructedMeshes</c>, and reseeds the campaign RNG only for that method.
/// </summary>
public static class SiegeSceneDestructionGate
{
    [ThreadStatic] private static Mission pendingMission;
    [ThreadStatic] private static uint? seed;

    /// <summary>[Game thread] Seed this siege mission's later scene build from its map-event id.</summary>
    public static void Begin(Mission mission, string mapEventId)
    {
        if (mission == null) throw new ArgumentNullException(nameof(mission));
        pendingMission = mission;
        seed = StableSeed(mapEventId);
    }

    /// <summary>[Game thread] Clear any pending scene-build seed.</summary>
    public static void End()
    {
        pendingMission = null;
        seed = null;
    }

    /// <summary>[Game thread] Consume the seed only for the mission it was bound to. Consumption is one-shot,
    /// so a second handler or a later mission cannot reuse it.</summary>
    public static bool TryTakeSeed(Mission mission, out uint value)
    {
        if (mission != null && ReferenceEquals(pendingMission, mission) && seed.HasValue)
        {
            value = seed.Value;
            End();
            return true;
        }

        value = 0u;
        return false;
    }

    // FNV-1a over the id's chars: stable across processes (unlike string.GetHashCode), so every client
    // derives the same seed from the same server-assigned map-event id.
    private static uint StableSeed(string id)
    {
        uint hash = 2166136261u;
        if (id != null)
        {
            foreach (char c in id)
            {
                hash ^= c;
                hash *= 16777619u;
            }
        }

        return hash == 0u ? 1u : hash;
    }
}
