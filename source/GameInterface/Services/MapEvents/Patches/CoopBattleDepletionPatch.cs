using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// In a coop field battle, determine side depletion (which drives victory/defeat in <c>BattleEndLogic</c>)
/// from the ACTUAL agents on the field, not the spawn logic's per-side count.
/// <para>
/// Each client only spawns the troops it owns, so <c>DefaultBattleMissionAgentSpawnLogic.NumberOfActiveTroops</c>
/// for a side it doesn't own is 0 even though that side is fully present as puppets (spawned via
/// <c>Mission.SpawnAgent</c>, which the spawn logic doesn't count). Native
/// <c>IsSideDepleted</c> = (one phase, NumberOfActiveTroops==0, RemainingSpawnNumber==0), so on a non-host the
/// enemy side reads "depleted" instantly → immediate false victory, and even the host's own side count is just
/// its own party (no client owns a whole side). Every client DOES have all agents (own + puppets), so counting
/// live agents per side is correct on all of them; a "has ever had agents" guard prevents declaring depletion
/// before a side's troops/puppets have spawned.
/// </para>
/// Only active in a coop battle; ordinary battles keep the native count-based check.
/// </summary>
[HarmonyPatch(typeof(DefaultBattleMissionAgentSpawnLogic), nameof(DefaultBattleMissionAgentSpawnLogic.IsSideDepleted))]
internal class CoopBattleDepletionPatch
{
    // Per spawn-logic (i.e. per mission): whether each side has ever had a live agent, so a side that simply
    // hasn't spawned/arrived yet isn't treated as depleted at the start.
    private static readonly ConditionalWeakTable<DefaultBattleMissionAgentSpawnLogic, bool[]> SideHadAgents = new();

    [HarmonyPrefix]
    private static bool Prefix(DefaultBattleMissionAgentSpawnLogic __instance, BattleSideEnum side, ref bool __result)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;
        if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender) return true;

        var mission = Mission.Current;
        if (mission == null) { __result = false; return false; }

        int active = 0;
        foreach (var team in mission.Teams)
        {
            if (team.Side != side) continue;
            foreach (var agent in team.ActiveAgents)
                if (agent.IsHuman) active++;
        }

        var had = SideHadAgents.GetValue(__instance, _ => new bool[2]);
        if (active > 0)
        {
            had[(int)side] = true;
            __result = false;
        }
        else
        {
            // No live agents: depleted only if this side had agents at some point (otherwise it just hasn't
            // spawned/arrived yet — e.g. the enemy puppets on a non-host before the catch-up burst lands),
            // or if the spawn handler deliberately crossed its reserve timeout for this exact side. The latter
            // is latched only after the full hold deadline; BattleEndLogic itself remains disabled until the
            // other side fields and deployment activates.
            __result = DetermineSideDepleted(had[(int)side], side);
        }
        return false; // skip the native count-based check
    }

    internal static bool DetermineSideDepleted(bool hadAgents, BattleSideEnum side)
    {
        if (BattleSpawnGate.HasPendingPrioritySpawn) return false;

        return hadAgents || BattleSpawnGate.IsMissingReserveSideAccepted(side);
    }
}
