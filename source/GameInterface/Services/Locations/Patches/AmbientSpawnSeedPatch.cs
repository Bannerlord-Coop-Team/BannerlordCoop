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
    // True only while the seeded spawn pass runs. CivilianAgentCountPinPatch reads it to override the
    // civilian-count config during that pass and leave it alone otherwise. [ThreadStatic] so it scopes
    // to the spawning (main) thread.
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

        if (!ModInformation.IsClient) return;
        if (Game.Current == null || RandomGeneratorProperty == null) return;
        if (!TryGetAmbientSeed(out var seed)) return;

        var previous = (MBFastRandom)RandomGeneratorProperty.GetValue(Game.Current);
        RandomGeneratorProperty.SetValue(Game.Current, new MBFastRandom(seed));

        AmbientPassActive = true;
        __state = new SeedScope { Active = true, Previous = previous };
    }

    // A finalizer so the campaign generator is always restored even when the spawn pass throws - a
    // skipped restore would leave the whole campaign running on the seeded generator.
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

        // A deterministic per-location key, distinct per location so the tavern and the town centre do
        // not share an identical crowd.
        seed = StableHash($"{settlement.StringId}_{location.StringId}");
        return true;
    }

    // Maps the per-location key to a seed that is identical on every machine. A runtime string hash is
    // randomized per process, so clients would derive different seeds and spawn different crowds.
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
