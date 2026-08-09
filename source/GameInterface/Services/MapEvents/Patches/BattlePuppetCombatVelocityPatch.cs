using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Native damage derives melee, missile, and horse-charge magnitude from attacker/victim relative velocity.
/// A remote victim's local puppet velocity can diverge from its owner's simulation, so substitute only that
/// victim contribution before native damage is calculated. Armor, weapons, perks, and the rest of the native
/// damage pipeline remain unchanged.
/// </summary>
[HarmonyPatch(
    typeof(MissionCombatMechanicsHelper),
    nameof(MissionCombatMechanicsHelper.GetAttackCollisionResults))]
internal static class BattlePuppetCombatVelocityPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref AttackInformation attackInformation)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive)
            return;

        Agent victim = attackInformation.VictimAgent;
        Vec2? globalVelocity = victim == null
            ? null
            : BattleSpawnGate.RemoteGlobalVelocityProbe?.Invoke(victim);
        if (!globalVelocity.HasValue)
            return;

        ApplyVictimGlobalVelocity(ref attackInformation, globalVelocity.Value);
    }

    internal static void ApplyVictimGlobalVelocity(
        ref AttackInformation attackInformation,
        Vec2 globalVelocity)
    {
        if (attackInformation.DoesVictimHaveMountAgent)
        {
            float speed = globalVelocity.Length;
            attackInformation.VictimAgentMovementVelocity = new Vec2(0f, speed);
            if (speed > 0.0001f)
                attackInformation.VictimAgentMountMovementDirection = globalVelocity / speed;
            return;
        }

        // Native rotates an on-foot/mount victim's local MovementVelocity by this angle. The owner sample is
        // already world-space, so a zero rotation reproduces the owner's velocity contribution directly.
        attackInformation.VictimAgentMovementVelocity = globalVelocity;
        attackInformation.VictimMovementDirectionAsAngle = 0f;
    }
}
