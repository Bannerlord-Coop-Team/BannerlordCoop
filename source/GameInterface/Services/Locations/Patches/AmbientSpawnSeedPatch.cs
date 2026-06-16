using Common;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Makes the scene-driven ambient crowd (townsfolk, villagers, merchants, workshop workers) identical
/// on every client. The spawning behaviors draw every per-NPC attribute from the global game RNG
/// (<see cref="MBRandom"/> -> <see cref="Game.RandomGenerator"/>), so left alone each client rolls a
/// different crowd. Around the synchronous <see cref="MissionAgentHandler.SpawnLocationCharacters"/>
/// pass this swaps in a generator seeded from a value derived purely from the location's identity, so
/// every client's pass draws the same sequence, then restores the campaign generator untouched.
/// The headless server never runs a mission, so this only ever takes effect on a visiting client.
/// </summary>
[HarmonyPatch(typeof(MissionAgentHandler), nameof(MissionAgentHandler.SpawnLocationCharacters))]
internal class AmbientSpawnSeedPatch
{
    // Set only while the seeded pass is running, so the count-pin patch knows when to fix the
    // per-player civilian-count config. Thread-scoped because the pass runs on the main thread and
    // the config getter must not be overridden for reads on any other thread.
    [ThreadStatic]
    internal static bool AmbientPassActive;

    private static readonly PropertyInfo RandomGeneratorProperty = AccessTools.Property(typeof(Game), "RandomGenerator");

    internal struct SeedScope
    {
        public bool Active;
        public MBFastRandom Previous;
    }

    static void Prefix(out SeedScope __state)
    {
        __state = default;

        // Only a client ever loads a settlement mission scene; in single player the mod is inert.
        if (!ModInformation.IsClient) return;
        if (Game.Current == null || RandomGeneratorProperty == null) return;
        if (!TryGetAmbientSeed(out var seed)) return;

        var previous = (MBFastRandom)RandomGeneratorProperty.GetValue(Game.Current);
        RandomGeneratorProperty.SetValue(Game.Current, new MBFastRandom(seed));

        AmbientPassActive = true;
        __state = new SeedScope { Active = true, Previous = previous };
    }

    // A finalizer (not a postfix) so the campaign generator is always restored even when the spawn
    // pass throws - a skipped restore would leave the whole campaign running on the seeded generator.
    static void Finalizer(SeedScope __state)
    {
        if (!__state.Active) return;

        AmbientPassActive = false;
        if (Game.Current != null)
        {
            RandomGeneratorProperty.SetValue(Game.Current, __state.Previous);
        }
    }

    private static bool TryGetAmbientSeed(out uint seed)
    {
        seed = 0;

        var settlement = Settlement.CurrentSettlement;
        var location = CampaignMission.Current?.Location;
        if (settlement == null || location == null) return false;

        // A deterministic per-location key: stable across machines (unlike string.GetHashCode) and
        // distinct per location so the tavern and the town centre do not share an identical crowd.
        seed = StableHash($"{settlement.StringId}_{location.StringId}");
        return true;
    }

    // FNV-1a: a fixed function of the bytes, identical on every machine and across runs.
    private static uint StableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var c in value)
        {
            hash = (hash ^ c) * prime;
        }

        return hash;
    }
}
