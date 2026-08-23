using HarmonyLib;
using SandBox;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// [Non-host / pre-assignment] Skips every native settlement population entry point while
/// <see cref="LocationNpcGate.ShouldSuppressNativeSpawns"/> holds (SR-012/SR-013): a coop location
/// mission is active and this client is not (yet) the confirmed NPC host. The host's assignment
/// lifts the gate and the population director runs the pass explicitly; non-hosts receive the
/// host's agents as replicated puppets instead. The sole exception is the local player's companion
/// roster entries: their owning client spawns those natively and replicates them as party puppets.
/// <para>
/// Seam inventory is decompile-verified (doc/SettlementRequirements.md V1–V3, R2). Every target is
/// void or null-tolerated at its call sites; <c>SpawnWanderingAgentWithInitialFrame</c> is
/// deliberately NOT skipped because callers dereference its return and the owner-side companion path
/// invokes it directly. Ambient access to it remains behind the gated entry points. Skipping
/// <c>SpawnLocationCharacters</c> wholesale also keeps the
/// <c>LocationCharactersAreReadyToSpawn</c> roster event from firing on non-hosts (R1): their
/// ambient roster entries are reconstructed from the host's spawn records, not rolled in parallel.
/// </para>
/// </summary>
[HarmonyPatch(typeof(MissionAgentHandler))]
internal class LocationNativeSpawnSuppressionPatches
{
    [HarmonyPatch(nameof(MissionAgentHandler.SpawnLocationCharacters))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipSpawnLocationCharacters() => !LocationNpcGate.ShouldSuppressNativeSpawns;

    [HarmonyPatch(nameof(MissionAgentHandler.SpawnDefaultLocationCharacter))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipSpawnDefaultLocationCharacter(LocationCharacter locationCharacter)
        => ShouldAllowDefaultLocationSpawn(
            LocationCharacterGuardPatches.IsLocalPlayerPartyCharacter(locationCharacter),
            LocationNpcGate.ShouldSuppressNativeSpawns);

    internal static bool ShouldAllowDefaultLocationSpawn(bool isOwnedCompanion, bool suppressNativeSpawns)
        => isOwnedCompanion || !suppressNativeSpawns;

    [HarmonyPatch(nameof(MissionAgentHandler.SpawnEnteringLocationCharacter))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipSpawnEnteringLocationCharacter(LocationCharacter locationCharacter)
        => ShouldAllowEnteringLocationSpawn(
            LocationCharacterGuardPatches.IsLocalPlayerPartyCharacter(locationCharacter),
            LocationNpcGate.ShouldSuppressNativeSpawns);

    internal static bool ShouldAllowEnteringLocationSpawn(bool isOwnedCompanion, bool suppressNativeSpawns)
        => isOwnedCompanion || !suppressNativeSpawns;

    [HarmonyPatch(nameof(MissionAgentHandler.SimulateAgent))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipPlayerPartySimulation(Agent agent)
        => ShouldSimulateAgent(LocationNpcGate.IsReplayingNativePopulation, agent);

    internal static bool ShouldSimulateAgent(bool isReplayingNativePopulation, Agent agent)
        => !isReplayingNativePopulation || !LocationNpcGate.IsPlayerPartyAgent(agent);

    [HarmonyPatch(nameof(MissionAgentHandler.SpawnWanderingAgentWithDelay))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipSpawnWanderingAgentWithDelay() => !LocationNpcGate.ShouldSuppressNativeSpawns;
}

/// <summary>
/// [Non-host / pre-assignment] Skips the settlement animal spawns (SR-020/V3). The helpers iterate
/// scene spawn tags (sp_horse/sp_sheep/…), so skipping them is a clean no-op for scenes without
/// animals; the host's pass captures + replicates the animals it spawns.
/// </summary>
[HarmonyPatch(typeof(SandBoxHelpers.MissionHelper))]
internal class LocationAnimalSpawnSuppressionPatches
{
    [HarmonyPatch(nameof(SandBoxHelpers.MissionHelper.SpawnHorses))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipSpawnHorses() => !LocationNpcGate.ShouldSuppressNativeSpawns;

    [HarmonyPatch(nameof(SandBoxHelpers.MissionHelper.SpawnSheeps))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipSpawnSheeps() => !LocationNpcGate.ShouldSuppressNativeSpawns;

    [HarmonyPatch(nameof(SandBoxHelpers.MissionHelper.SpawnCows))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipSpawnCows() => !LocationNpcGate.ShouldSuppressNativeSpawns;

    [HarmonyPatch(nameof(SandBoxHelpers.MissionHelper.SpawnHogs))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipSpawnHogs() => !LocationNpcGate.ShouldSuppressNativeSpawns;

    [HarmonyPatch(nameof(SandBoxHelpers.MissionHelper.SpawnGeese))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipSpawnGeese() => !LocationNpcGate.ShouldSuppressNativeSpawns;

    [HarmonyPatch(nameof(SandBoxHelpers.MissionHelper.SpawnChicken))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool SkipSpawnChicken() => !LocationNpcGate.ShouldSuppressNativeSpawns;
}

/// <summary>
/// [Non-host / pre-assignment] Skips the 30-second passage-usage tick (R2): it picks a RANDOM
/// non-fixed roster character and moves it between locations, so on a non-host it would mutate the
/// roster with unsynchronized local RNG. On the host it runs natively — its arrivals/exits replicate
/// as spawn/despawn records (SR-026).
/// </summary>
[HarmonyPatch(typeof(LocationComplex), nameof(LocationComplex.AgentPassageUsageTick))]
internal class LocationPassageTickSuppressionPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static bool Prefix() => !LocationNpcGate.ShouldSuppressNativeSpawns;
}
