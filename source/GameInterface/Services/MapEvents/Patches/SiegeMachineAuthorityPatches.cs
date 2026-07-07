using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Keeps every siege machine simulated by exactly one client. A machine's vanilla state (aim, fire,
/// ram advance, gate hits) is driven by whatever agents man it locally, so with players on both
/// machines the same ballista would run two divergent simulations. The mission host simulates all
/// machines; the other clients' troop AI never mans them and their local gate toggles are blocked —
/// the authoritative state arrives through the machine state replication.
/// </summary>
[HarmonyPatch]
internal class SiegeMachineAuthorityPatches
{
    // ForcedUse: the per-tick scan that sends the local side's troops to man undermanned machines.
    [HarmonyPatch(typeof(SiegeWeapon), nameof(SiegeWeapon.TickAux))]
    [HarmonyPrefix]
    private static bool TickAuxPrefix()
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;

        return SiegeMissionAuthorityGate.IsLocalAuthority;
    }

    [HarmonyPatch(typeof(CastleGate), nameof(CastleGate.OpenDoor))]
    [HarmonyPrefix]
    private static bool OpenDoorPrefix() => AllowGateToggle();

    [HarmonyPatch(typeof(CastleGate), nameof(CastleGate.CloseDoor))]
    [HarmonyPrefix]
    private static bool CloseDoorPrefix() => AllowGateToggle();

    // The gate's auto-open logic reacts to nearby agents, including interpolated puppets, so a
    // non-authority client's local toggles must not run; the replicated state applies under suppress.
    private static bool AllowGateToggle()
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;
        if (SiegeMissionAuthorityGate.SuppressCapture) return true;

        return SiegeMissionAuthorityGate.IsLocalAuthority;
    }
}
