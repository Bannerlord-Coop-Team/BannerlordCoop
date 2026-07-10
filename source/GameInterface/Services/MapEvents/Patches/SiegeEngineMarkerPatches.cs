using HarmonyLib;
using System.Linq;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD.FormationMarker;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// [Client] Drops disabled machines from the Alt-overlay siege engine markers. The vanilla view
/// snapshots the machine list once at the local deployment finish and only ever removes destroyed
/// machines — on a non-deployer the deferred sweep disables the hidden undeployed sibling weapons
/// AFTER that snapshot, leaving phantom icons over empty placement spots for the whole battle.
/// </summary>
[HarmonyPatch(typeof(MissionSiegeEngineMarkerVM), "RefreshSiegeEngineList")]
internal class SiegeEngineMarkerPatches
{
    [HarmonyPostfix]
    private static void RefreshSiegeEngineListPostfix(MissionSiegeEngineMarkerVM __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;

        for (int i = __instance._siegeEngines.Count - 1; i >= 0; i--)
        {
            var engine = __instance._siegeEngines[i];
            if (!engine.IsDisabled) continue;

            __instance._siegeEngines.RemoveAt(i);
            var target = __instance.Targets.SingleOrDefault(candidate => candidate.Engine == engine);
            __instance.Targets.Remove(target);
        }
    }
}
