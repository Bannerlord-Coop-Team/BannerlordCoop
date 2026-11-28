using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>Uses the replicated retreat flag when a coop battle checks for nearby enemies.</summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.IsPlayerCloseToAnEnemy))]
internal class BattleEnemyProximityPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var isRetreating = AccessTools.Method(typeof(Agent), nameof(Agent.IsRetreating));
        var isRetreatingForProximity = AccessTools.Method(typeof(BattleEnemyProximityPatch), nameof(IsRetreatingForProximity));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(isRetreating))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = isRetreatingForProximity;
            }
            yield return instruction;
        }
    }

    private static bool IsRetreatingForProximity(Agent agent)
    {
        // A routed puppet can still lack native retreat mode while remote or just adopted.
        if (agent.IsRunningAway && BattleSpawnConfig.Enabled && BattleSpawnGate.IsCoopBattleActive)
            return true;

        return agent.IsRetreating();
    }
}
