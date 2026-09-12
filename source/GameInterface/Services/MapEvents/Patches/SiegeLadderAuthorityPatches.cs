using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class SiegeLadderAuthorityPatches
{
    private const int LocalMaintenanceClientChecks = 4;
    private const int ExpectedOnTickClientChecks = 8;

    [HarmonyPatch(typeof(SiegeLadder), nameof(SiegeLadder.State), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool StateSetterPrefix(SiegeLadder __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;
        if (SiegeMissionAuthorityGate.SuppressCapture) return true;
        if (!SiegeMissionAuthorityGate.IsAuthorityKnown) return false;

        return SiegeMissionAuthorityGate.IsMachineSimulatedLocally(__instance.Id.Id);
    }

    [HarmonyPatch(typeof(SiegeLadder), "OnTick")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> OnTickTranspiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        var nativeClientGetter = AccessTools.PropertyGetter(typeof(GameNetwork), nameof(GameNetwork.IsClientOrReplay));
        var coopClientGetter = AccessTools.Method(typeof(SiegeLadderAuthorityPatches), nameof(IsClientForSiegeLadder));
        int clientChecks = 0;
        int existingCoopChecks = 0;

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(coopClientGetter))
            {
                existingCoopChecks++;
                yield return instruction;
                continue;
            }

            if (!instruction.Calls(nativeClientGetter))
            {
                yield return instruction;
                continue;
            }

            clientChecks++;
            // The first four guards maintain local queues and standing points; later guards drive shared animation.
            if (clientChecks <= LocalMaintenanceClientChecks)
            {
                yield return instruction;
                continue;
            }

            instruction.opcode = OpCodes.Ldarg_0;
            instruction.operand = null;
            yield return instruction;
            yield return new CodeInstruction(OpCodes.Call, coopClientGetter);
        }

        bool firstApplication = clientChecks == ExpectedOnTickClientChecks && existingCoopChecks == 0;
        bool repeatedApplication = clientChecks == LocalMaintenanceClientChecks
            && existingCoopChecks == ExpectedOnTickClientChecks - LocalMaintenanceClientChecks;
        if (!firstApplication && !repeatedApplication)
        {
            throw new InvalidOperationException(
                $"Failed to patch siege ladder authority checks in {__originalMethod.Name}: " +
                $"found {clientChecks} native and {existingCoopChecks} co-op checks.");
        }
    }

    [HarmonyPatch(typeof(SiegeLadder), "OnLadderStateChange")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> OnLadderStateChangeTranspiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        return UseCoopClientPath(instructions, __originalMethod);
    }

    // Received states need vanilla's client animation path while the elected mission host advances the ladder.
    private static IEnumerable<CodeInstruction> UseCoopClientPath(
        IEnumerable<CodeInstruction> instructions,
        MethodBase originalMethod)
    {
        var nativeClientGetter = AccessTools.PropertyGetter(typeof(GameNetwork), nameof(GameNetwork.IsClientOrReplay));
        var coopClientGetter = AccessTools.Method(typeof(SiegeLadderAuthorityPatches), nameof(IsClientForSiegeLadder));
        int replacements = 0;
        int existingReplacements = 0;

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(coopClientGetter))
            {
                existingReplacements++;
                yield return instruction;
                continue;
            }

            if (!instruction.Calls(nativeClientGetter))
            {
                yield return instruction;
                continue;
            }

            instruction.opcode = OpCodes.Ldarg_0;
            instruction.operand = null;
            yield return instruction;
            yield return new CodeInstruction(OpCodes.Call, coopClientGetter);
            replacements++;
        }

        if (replacements == 0 && existingReplacements == 0)
        {
            throw new InvalidOperationException(
                $"Failed to patch siege ladder authority checks in {originalMethod.Name}.");
        }
    }

    private static bool IsClientForSiegeLadder(SiegeLadder ladder)
    {
        if (GameNetwork.IsClientOrReplay) return true;
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return false;
        if (!SiegeMissionAuthorityGate.IsAuthorityKnown) return true;

        return !SiegeMissionAuthorityGate.IsMachineSimulatedLocally(ladder.Id.Id);
    }
}
