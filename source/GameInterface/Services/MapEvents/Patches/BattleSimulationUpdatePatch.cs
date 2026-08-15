using Common;
using GameInterface.Services.MapEvents.Initialization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(BattleSimulation))]
public class BattleSimulationUpdatePatch
{
    /// <summary>
    /// The simulation engine is authoritative on the server. Clients never run it locally
    /// (it rolls <c>MBRandom</c> and mutates rosters); instead they request the server to run it and
    /// replay the server's per-round results onto the scoreboard at the normal cadence. The server
    /// still ticks its own simulation normally.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(nameof(BattleSimulation.Tick))]
    static bool PrefixUpdate(BattleSimulation __instance, float dt)
    {
        if (ModInformation.IsServer)
            return true;

        BattleSimulationReplay.Tick(__instance, dt);
        return false;
    }
}

[HarmonyPatch(typeof(MapState), nameof(MapState.EndBattleSimulation))]
internal class BattleSimulationEndPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ModInformation.IsServer) return;

        // Scoreboard Done calls this directly, so finish retained teardown before the encounter menu resumes.
        if (ContainerProvider.TryResolve<IMapEventInitializationBarrier>(out var barrier))
            barrier.CompleteDeferredEncounterCleanup();
    }
}
