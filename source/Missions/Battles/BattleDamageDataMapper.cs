using Missions.Messages;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Converts routed damage between engine structs and the owned network representation.</summary>
public interface IBattleDamageDataMapper
{
    BattleDamageData Pack(in Blow blow, in AttackCollisionData collisionData);
    bool TryResolve(
        BattleDamageData damageData,
        out Blow blow,
        out AttackCollisionData collisionData);
}

/// <summary>Maps routed-damage engine structs to a versioned, field-owned wire representation.</summary>
public class BattleDamageDataMapper : IBattleDamageDataMapper
{
    public BattleDamageData Pack(in Blow blow, in AttackCollisionData collisionData)
    {
        return new BattleDamageData(
            new BattleBlowData(blow),
            new BattleCollisionData(collisionData));
    }

    public bool TryResolve(
        BattleDamageData damageData,
        out Blow blow,
        out AttackCollisionData collisionData)
    {
        blow = default;
        collisionData = default;
        if (damageData == null ||
            damageData.Version != BattleDamageData.CurrentVersion ||
            damageData.Blow == null ||
            damageData.Collision == null)
        {
            return false;
        }

        BattleBlowData blowData = damageData.Blow;
        blow = new Blow(blowData.OwnerId)
        {
            WeaponRecord = new BlowWeaponRecord
            {
                StartingPosition = blowData.WeaponStartingPosition,
                CurrentPosition = blowData.WeaponCurrentPosition,
                Velocity = blowData.WeaponVelocity,
                ItemFlags = blowData.ItemFlags,
                WeaponFlags = blowData.WeaponFlags,
                WeaponClass = blowData.WeaponClass,
                BoneNoToAttach = blowData.WeaponBoneNoToAttach,
                AffectorWeaponSlotOrMissileIndex = blowData.AffectorWeaponSlotOrMissileIndex,
                Weight = blowData.WeaponWeight,
                _isMissile = blowData.IsMissile,
                _isMaterialMetal = blowData.IsMaterialMetal,
            },
            GlobalPosition = blowData.GlobalPosition,
            Direction = blowData.Direction,
            SwingDirection = blowData.SwingDirection,
            InflictedDamage = blowData.InflictedDamage,
            SelfInflictedDamage = blowData.SelfInflictedDamage,
            BaseMagnitude = blowData.BaseMagnitude,
            DefenderStunPeriod = blowData.DefenderStunPeriod,
            AttackerStunPeriod = blowData.AttackerStunPeriod,
            AbsorbedByArmor = blowData.AbsorbedByArmor,
            MovementSpeedDamageModifier = blowData.MovementSpeedDamageModifier,
            StrikeType = blowData.StrikeType,
            AttackType = blowData.AttackType,
            BlowFlag = blowData.BlowFlag,
            BoneIndex = blowData.BoneIndex,
            VictimBodyPart = blowData.VictimBodyPart,
            DamageType = blowData.DamageType,
            NoIgnore = blowData.NoIgnore,
            DamageCalculated = blowData.DamageCalculated,
            IsFallDamage = blowData.IsFallDamage,
            DamagedPercentage = blowData.DamagedPercentage,
        };

        BattleCollisionData collision = damageData.Collision;
        collisionData = new AttackCollisionData(
            collision.AttackBlockedWithShield,
            collision.CorrectSideShieldBlock,
            collision.IsAlternativeAttack,
            collision.IsColliderAgent,
            collision.CollidedWithShieldOnBack,
            collision.IsMissile,
            collision.MissileBlockedWithWeapon,
            collision.MissileHasPhysics,
            collision.EntityExists,
            collision.ThrustTipHit,
            collision.MissileGoneUnderWater,
            collision.MissileGoneOutOfBorder,
            collision.CollidedWithLastBoneSegment,
            collision.CollisionResult,
            collision.AffectorWeaponSlotOrMissileIndex,
            collision.StrikeType,
            collision.DamageType,
            collision.CollisionBoneIndex,
            collision.VictimHitBodyPart,
            collision.AttackBoneIndex,
            collision.AttackDirection,
            collision.PhysicsMaterialIndex,
            collision.CollisionHitResultFlags,
            collision.AttackProgress,
            collision.CollisionDistanceOnWeapon,
            collision.AttackerStunPeriod,
            collision.DefenderStunPeriod,
            collision.MissileTotalDamage,
            collision.MissileStartingBaseSpeed,
            collision.ChargeVelocity,
            collision.FallSpeed,
            collision.WeaponRotUp,
            collision.WeaponBlowDir,
            collision.CollisionGlobalPosition,
            collision.MissileVelocity,
            collision.MissileStartingPosition,
            collision.VictimAgentCurVelocity,
            collision.CollisionGlobalNormal,
            collision.LastBoneSegmentRotUp,
            collision.LastBoneSegmentSwingDir)
        {
            BaseMagnitude = collision.BaseMagnitude,
            MovementSpeedDamageModifier = collision.MovementSpeedDamageModifier,
            AbsorbedByArmor = collision.AbsorbedByArmor,
            InflictedDamage = collision.InflictedDamage,
            SelfInflictedDamage = collision.SelfInflictedDamage,
            IsShieldBroken = collision.IsShieldBroken,
            IsSneakAttack = collision.IsSneakAttack,
        };
        return true;
    }
}
