using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Keeps every siege machine simulated by exactly one client. A machine's vanilla state (aim, fire,
/// ram advance, gate hits) is driven by whatever agents man it locally, so with players on both
/// machines the same ballista would run two divergent simulations. The mission host simulates a
/// machine unless a client claimed it by manning it; everyone else's troop AI never mans it and
/// their local gate toggles are blocked — the authoritative state arrives through the machine
/// state replication.
/// </summary>
[HarmonyPatch]
internal class SiegeMachineAuthorityPatches
{
    // ForcedUse: the per-tick scan that sends the local side's troops to man undermanned machines.
    [HarmonyPatch(typeof(SiegeWeapon), nameof(SiegeWeapon.TickAux))]
    [HarmonyPrefix]
    private static bool TickAuxPrefix(SiegeWeapon __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;

        return SiegeMissionAuthorityGate.IsMachineSimulatedLocally(__instance.Id.Id);
    }

    [HarmonyPatch(typeof(StonePile), "OnTick")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> StonePileOnTickTranspiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        var nativeClientGetter = AccessTools.PropertyGetter(typeof(GameNetwork), nameof(GameNetwork.IsClientOrReplay));
        var coopClientGetter = AccessTools.Method(typeof(SiegeMachineAuthorityPatches), nameof(IsClientForStonePile));
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

        if (replacements != 1 && !(replacements == 0 && existingReplacements == 1))
        {
            throw new InvalidOperationException(
                $"Failed to patch stone pile authority check in {__originalMethod.Name}: " +
                $"found {replacements} native and {existingReplacements} co-op checks.");
        }
    }

    private static bool IsClientForStonePile(StonePile stonePile)
    {
        if (GameNetwork.IsClientOrReplay) return true;
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return false;
        if (!SiegeMissionAuthorityGate.IsAuthorityKnown) return true;

        return !SiegeMissionAuthorityGate.IsMachineSimulatedLocally(stonePile.Id.Id);
    }

    // A machine simulated elsewhere aims from the replicated targets: re-asserting them each weapon
    // tick lets vanilla's own speed-limited approach turn the body smoothly, with no pilot, no shot
    // and no state writes.
    [HarmonyPatch(typeof(RangedSiegeWeapon), "HandleUserAiming")]
    [HarmonyPrefix]
    private static void HandleUserAimingPrefix(RangedSiegeWeapon __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;
        if (!SiegeMissionAuthorityGate.TryGetRemoteAim(__instance.Id.Id, out var direction, out var releaseAngle)) return;

        __instance.TargetDirection = direction;
        __instance.TargetReleaseAngle = releaseAngle;
    }

    // A player can mount a machine before its claim round-trips; the local sim must not launch an
    // unreplicated shot in that window (the machine's replicated state covers everything else).
    [HarmonyPatch(typeof(RangedSiegeWeapon), nameof(RangedSiegeWeapon.Shoot))]
    [HarmonyPrefix]
    private static bool ShootPrefix(RangedSiegeWeapon __instance, ref bool __result)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;
        if (SiegeMissionAuthorityGate.IsMachineSimulatedLocally(__instance.Id.Id)) return true;

        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(CastleGate), nameof(CastleGate.OpenDoor))]
    [HarmonyPrefix]
    private static bool OpenDoorPrefix(CastleGate __instance) => AllowGateToggle(__instance);

    [HarmonyPatch(typeof(CastleGate), nameof(CastleGate.CloseDoor))]
    [HarmonyPrefix]
    private static bool CloseDoorPrefix(CastleGate __instance) => AllowGateToggle(__instance);

    [HarmonyPatch(typeof(CastleGate), "SetAutoOpenState")]
    [HarmonyPrefix]
    private static bool SetAutoOpenStatePrefix(CastleGate __instance) => AllowGateToggle(__instance);

    // The gate's auto-open logic reacts to nearby agents, including interpolated puppets, so a
    // non-authority client's local toggles must not run; the replicated state applies under suppress.
    private static bool AllowGateToggle(CastleGate gate)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;
        if (!SiegeMissionAuthorityGate.IsAuthorityKnown) return true;
        if (SiegeMissionAuthorityGate.SuppressCapture) return true;

        return SiegeMissionAuthorityGate.IsMachineSimulatedLocally(gate.Id.Id);
    }
}
