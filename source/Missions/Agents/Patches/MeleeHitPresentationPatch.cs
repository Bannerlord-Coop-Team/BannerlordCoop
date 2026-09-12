using Common.Messaging;
using HarmonyLib;
using Missions.Agents.Extensions;
using Missions.Agents.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches;

/// <summary>Captures presentation selected by the native collision-authority peer.</summary>
[HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
[HarmonyPatchCategory(MissionModule.CombatHitPresentationPatchCategory)]
public static class MeleeHitPresentationPatch
{
    private static void Postfix(
        ref AttackCollisionData collisionData,
        Agent attacker,
        Agent victim)
    {
        if (victim == null ||
            attacker == null ||
            Mission.Current?.GetMissionBehavior<CoopMissionController>() == null ||
            !attacker.IsLocallyControlled())
        {
            return;
        }

        MissionWeapon weapon = ResolveWeapon(attacker, collisionData.AffectorWeaponSlotOrMissileIndex);
        WeaponClass weaponClass = weapon.IsEmpty
            ? WeaponClass.Undefined
            : weapon.CurrentUsageItem.WeaponClass;

        if (collisionData.AttackBlockedWithShield)
        {
            Publish(
                victim,
                MeleeHitPresentationKind.ShieldImpact,
                in collisionData,
                weaponClass,
                Clamp(collisionData.AttackProgress, 0.2f, 1f));
        }
    }

    private static MissionWeapon ResolveWeapon(Agent attacker, int slotIndex)
    {
        if (attacker?.Equipment == null ||
            slotIndex < 0 ||
            slotIndex >= (int)EquipmentIndex.NumAllWeaponSlots)
        {
            return MissionWeapon.Invalid;
        }

        return attacker.Equipment[(EquipmentIndex)slotIndex];
    }

    private static void Publish(
        Agent victim,
        MeleeHitPresentationKind kind,
        in AttackCollisionData collisionData,
        WeaponClass weaponClass,
        float strength)
    {
        MessageBroker.Instance.Publish(
            victim,
            new MeleeHitPresentation(
                victim,
                kind,
                collisionData.CollisionBoneIndex,
                collisionData.CollisionGlobalPosition,
                weaponClass,
                collisionData.PhysicsMaterialIndex,
                strength));
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return minimum;
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }
}
