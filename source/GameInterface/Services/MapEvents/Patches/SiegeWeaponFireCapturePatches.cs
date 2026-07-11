using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Marks the window in which a ranged siege weapon is launching its projectile on the host, so the shared
/// AddCustomMissile path can tell a siege shot apart. Game-thread only (missions tick there), hence ThreadStatic.
/// </summary>
internal static class SiegeWeaponFireContext
{
    [System.ThreadStatic]
    public static RangedSiegeWeapon Capturing;
}

/// <summary>
/// [Owner] Brackets <see cref="RangedSiegeWeapon.ShootProjectileAux"/> so the AddCustomMissile it calls is
/// recognised as this weapon's shot. Only the machine's simulating client captures.
/// </summary>
[HarmonyPatch(typeof(RangedSiegeWeapon), "ShootProjectileAux")]
internal static class RangedSiegeWeaponShootPatch
{
    private static void Prefix(RangedSiegeWeapon __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;
        if (!SiegeMissionAuthorityGate.IsMachineSimulatedLocally(__instance.Id.Id)) return;

        SiegeWeaponFireContext.Capturing = __instance;
    }

    // Clear even if the launch throws before AddCustomMissile, so a stale weapon can't taint a later shot.
    private static void Finalizer()
    {
        SiegeWeaponFireContext.Capturing = null;
    }
}

/// <summary>
/// [Host] Captures the resolved launch of a siege weapon's projectile and publishes it for replication. Fires
/// only while a <see cref="RangedSiegeWeapon"/> shot is in flight (the context is set), so other custom
/// missiles are ignored.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.AddCustomMissile))]
internal static class SiegeWeaponFireCapturePatch
{
    private static void Postfix(Agent shooterAgent, MissionWeapon missileWeapon, Vec3 position, Vec3 direction, Mat3 orientation, float baseSpeed, float speed)
    {
        var weapon = SiegeWeaponFireContext.Capturing;
        if (weapon == null) return;

        // One projectile per shot; clear before publishing so any nested aux-missile patches stop matching.
        SiegeWeaponFireContext.Capturing = null;

        MessageBroker.Instance.Publish(weapon, new SiegeWeaponFired(weapon, shooterAgent, position, direction, orientation, baseSpeed, speed, missileWeapon.Item));
    }
}
