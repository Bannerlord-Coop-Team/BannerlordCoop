using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Uses the owner's killed/unconscious result when native death handling removes a replicated puppet.
/// </summary>
[HarmonyPatch(typeof(Mission), "GetAgentState",
    new[] { typeof(Agent), typeof(Agent), typeof(DamageTypes), typeof(WeaponFlags) })]
internal class ReplicatedDeathAgentStatePatch
{
    [HarmonyPrefix]
    internal static bool Prefix(Agent agent, ref AgentState __result)
    {
        if (!BattleSpawnConfig.Enabled) return true;
        if (!BattleSpawnGate.IsCoopBattleActive) return true;
        if (!BattleSpawnGate.TryGetReplicatedDeathState(agent, out var agentState)) return true;

        __result = agentState;
        return false;
    }
}
