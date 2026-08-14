#if DEBUG
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

[HarmonyPatch(
    typeof(Mission),
    nameof(Mission.OnTick),
    new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) })]
[HarmonyPatchCategory(MissionModule.LiveTestInputPatchCategory)]
internal static class JoustInputBoundaryPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo asyncTick = AccessTools.Method(
            typeof(Mission),
            nameof(Mission.TickAgentsAndTeamsAsync),
            new[] { typeof(float) });
        MethodInfo directTick = AccessTools.Method(
            typeof(Mission),
            nameof(Mission.TickAgentsAndTeamsImp),
            new[] { typeof(float), typeof(bool) });
        MethodInfo applyInput = AccessTools.Method(
            typeof(BattleDebugCommands),
            nameof(BattleDebugCommands.ApplyJoustInputAtNativeTickBoundary));
        int boundaryCount = 0;

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(asyncTick) || instruction.Calls(directTick))
            {
                var loadMission = new CodeInstruction(OpCodes.Ldarg_0);
                loadMission.MoveLabelsFrom(instruction);
                yield return loadMission;
                yield return new CodeInstruction(OpCodes.Call, applyInput);
                boundaryCount++;
            }

            yield return instruction;
        }

        if (boundaryCount != 2)
        {
            throw new InvalidOperationException(
                $"Expected two Mission agent-tick dispatches, found {boundaryCount}.");
        }
    }
}

[HarmonyPatch(
    typeof(InputContext),
    nameof(InputContext.GetGameKeyAxis),
    new[] { typeof(string) })]
[HarmonyPatchCategory(MissionModule.LiveTestInputPatchCategory)]
internal static class JoustMovementAxisInputPatch
{
    [HarmonyPostfix]
    internal static void Postfix(
        InputContext __instance,
        string gameKey,
        ref float __result)
    {
        if (BattleDebugCommands.TryGetJoustMovementAxis(
                __instance,
                gameKey,
                out float value))
        {
            __result = value;
        }
    }
}

[HarmonyPatch(
    typeof(InputContext),
    nameof(InputContext.IsGameKeyDown),
    new[] { typeof(int) })]
[HarmonyPatchCategory(MissionModule.LiveTestInputPatchCategory)]
internal static class JoustAttackInputPatch
{
    [HarmonyPostfix]
    internal static void Postfix(
        InputContext __instance,
        int gameKey,
        ref bool __result)
    {
        if (BattleDebugCommands.ShouldHoldJoustAttack(
                __instance,
                gameKey))
        {
            __result = true;
        }
    }
}
#endif
