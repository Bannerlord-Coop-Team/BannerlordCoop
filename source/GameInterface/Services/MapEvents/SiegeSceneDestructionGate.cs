using System;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Deterministic seed for the vanilla siege scene's random wall dressing. Vanilla
/// <c>SiegeMissionPreparationHandler.ArrangeDestructedMeshes</c> pre-destroys a RANDOM subset of the
/// destructible wall pieces (chunks, merlons, rubble) and reveals random damage decals via
/// <c>MBRandom</c> = <c>Game.Current.RandomGenerator</c>, whose state differs per machine and never
/// replicates in coop (no GameNetwork session). So each client breaks different wall pieces at scene
/// build. The launcher seeds this from the map-event id (identical on every client) around
/// <c>MissionState.OpenNew</c>, and <c>SiegeDestructionSeedPatch</c> reseeds the campaign RNG from it
/// for the duration of that one method so every client makes the same picks.
/// </summary>
public static class SiegeSceneDestructionGate
{
    [ThreadStatic] private static uint? seed;

    /// <summary>[Game thread] Seed the next siege scene build from its map-event id.</summary>
    public static void Begin(string mapEventId) => seed = StableSeed(mapEventId);

    /// <summary>[Game thread] Clear the seed once the scene has built.</summary>
    public static void End() => seed = null;

    public static bool TryGetSeed(out uint value)
    {
        if (seed.HasValue)
        {
            value = seed.Value;
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
