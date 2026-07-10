using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Vanilla siege scene setup pre-destroys a RANDOM subset of wall pieces and reveals random damage decals
/// (ArrangeDestructedMeshes -> MBRandom -> Game.Current.RandomGenerator), so each coop client breaks
/// different wall pieces at load with nothing to replicate it — the defender still sees a wall the attacker
/// already broke. Reseed the campaign RNG from the shared map-event seed for the duration of that one method
/// so every client makes the same picks, then restore it. A finalizer restores even if the method throws, so
/// the campaign RNG stream is never left reseeded. The real breached wall SECTIONS are chosen deterministically
/// from the synced hit-point ratios and are untouched.
/// </summary>
[HarmonyPatch(typeof(SiegeMissionPreparationHandler), "ArrangeDestructedMeshes")]
internal static class SiegeDestructionSeedPatch
{
    [HarmonyPrefix]
    private static void Prefix(out MBFastRandom __state)
    {
        __state = null;
        if (Game.Current == null) return;
        if (!SiegeSceneDestructionGate.TryGetSeed(out var seed)) return;

        __state = Game.Current.RandomGenerator;
        Game.Current.RandomGenerator = new MBFastRandom(seed);
    }

    [HarmonyFinalizer]
    private static void Finalizer(MBFastRandom __state)
    {
        if (__state != null && Game.Current != null)
            Game.Current.RandomGenerator = __state;
    }
}
