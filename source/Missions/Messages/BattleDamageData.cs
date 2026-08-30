using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>Owned, versioned wire representation of the engine structs needed to replay one routed blow.</summary>
[ProtoContract(SkipConstructor = true)]
public class BattleDamageData
{
    public const int CurrentVersion = 1;

    [ProtoMember(1)]
    public readonly int Version = CurrentVersion;
    [ProtoMember(2)]
    public readonly BattleBlowData Blow;
    [ProtoMember(3)]
    public readonly BattleCollisionData Collision;

    public BattleDamageData(BattleBlowData blow, BattleCollisionData collision)
    {
        Blow = blow;
        Collision = collision;
    }
}

/// <summary>Field-owned wire representation of <see cref="Blow"/> and its weapon record.</summary>
[ProtoContract(SkipConstructor = true)]
public class BattleBlowData
{
    [ProtoMember(1)] public readonly Vec3 WeaponStartingPosition;
    [ProtoMember(2)] public readonly Vec3 WeaponCurrentPosition;
    [ProtoMember(3)] public readonly Vec3 WeaponVelocity;
    [ProtoMember(4)] public readonly ItemFlags ItemFlags;
    [ProtoMember(5)] public readonly WeaponFlags WeaponFlags;
    [ProtoMember(6)] public readonly WeaponClass WeaponClass;
    [ProtoMember(7)] public readonly sbyte WeaponBoneNoToAttach;
    [ProtoMember(8)] public readonly int AffectorWeaponSlotOrMissileIndex;
    [ProtoMember(9)] public readonly float WeaponWeight;
    [ProtoMember(10)] public readonly bool IsMissile;
    [ProtoMember(11)] public readonly bool IsMaterialMetal;
    [ProtoMember(12)] public readonly Vec3 GlobalPosition;
    [ProtoMember(13)] public readonly Vec3 Direction;
    [ProtoMember(14)] public readonly Vec3 SwingDirection;
    [ProtoMember(15)] public readonly int InflictedDamage;
    [ProtoMember(16)] public readonly int SelfInflictedDamage;
    [ProtoMember(17)] public readonly float BaseMagnitude;
    [ProtoMember(18)] public readonly float DefenderStunPeriod;
    [ProtoMember(19)] public readonly float AttackerStunPeriod;
    [ProtoMember(20)] public readonly float AbsorbedByArmor;
    [ProtoMember(21)] public readonly float MovementSpeedDamageModifier;
    [ProtoMember(22)] public readonly StrikeType StrikeType;
    [ProtoMember(23)] public readonly AgentAttackType AttackType;
    [ProtoMember(24)] public readonly BlowFlags BlowFlag;
    [ProtoMember(25)] public readonly int OwnerId;
    [ProtoMember(26)] public readonly sbyte BoneIndex;
    [ProtoMember(27)] public readonly BoneBodyPartType VictimBodyPart;
    [ProtoMember(28)] public readonly DamageTypes DamageType;
    [ProtoMember(29)] public readonly bool NoIgnore;
    [ProtoMember(30)] public readonly bool DamageCalculated;
    [ProtoMember(31)] public readonly bool IsFallDamage;
    [ProtoMember(32)] public readonly float DamagedPercentage;

    public BattleBlowData(Blow blow)
    {
        BlowWeaponRecord weapon = blow.WeaponRecord;
        WeaponStartingPosition = weapon.StartingPosition;
        WeaponCurrentPosition = weapon.CurrentPosition;
        WeaponVelocity = weapon.Velocity;
        ItemFlags = weapon.ItemFlags;
        WeaponFlags = weapon.WeaponFlags;
        WeaponClass = weapon.WeaponClass;
        WeaponBoneNoToAttach = weapon.BoneNoToAttach;
        AffectorWeaponSlotOrMissileIndex = weapon.AffectorWeaponSlotOrMissileIndex;
        WeaponWeight = weapon.Weight;
        IsMissile = weapon.IsMissile;
        IsMaterialMetal = weapon._isMaterialMetal;
        GlobalPosition = blow.GlobalPosition;
        Direction = blow.Direction;
        SwingDirection = blow.SwingDirection;
        InflictedDamage = blow.InflictedDamage;
        SelfInflictedDamage = blow.SelfInflictedDamage;
        BaseMagnitude = blow.BaseMagnitude;
        DefenderStunPeriod = blow.DefenderStunPeriod;
        AttackerStunPeriod = blow.AttackerStunPeriod;
        AbsorbedByArmor = blow.AbsorbedByArmor;
        MovementSpeedDamageModifier = blow.MovementSpeedDamageModifier;
        StrikeType = blow.StrikeType;
        AttackType = blow.AttackType;
        BlowFlag = blow.BlowFlag;
        OwnerId = blow.OwnerId;
        BoneIndex = blow.BoneIndex;
        VictimBodyPart = blow.VictimBodyPart;
        DamageType = blow.DamageType;
        NoIgnore = blow.NoIgnore;
        DamageCalculated = blow.DamageCalculated;
        IsFallDamage = blow.IsFallDamage;
        DamagedPercentage = blow.DamagedPercentage;
    }
}

/// <summary>Field-owned wire representation of <see cref="AttackCollisionData"/>.</summary>
[ProtoContract(SkipConstructor = true)]
public class BattleCollisionData
{
    [ProtoMember(1)] public readonly bool AttackBlockedWithShield;
    [ProtoMember(2)] public readonly bool CorrectSideShieldBlock;
    [ProtoMember(3)] public readonly bool IsAlternativeAttack;
    [ProtoMember(4)] public readonly bool IsColliderAgent;
    [ProtoMember(5)] public readonly bool CollidedWithShieldOnBack;
    [ProtoMember(6)] public readonly bool IsMissile;
    [ProtoMember(7)] public readonly bool MissileBlockedWithWeapon;
    [ProtoMember(8)] public readonly bool MissileHasPhysics;
    [ProtoMember(9)] public readonly bool EntityExists;
    [ProtoMember(10)] public readonly bool ThrustTipHit;
    [ProtoMember(11)] public readonly bool MissileGoneUnderWater;
    [ProtoMember(12)] public readonly bool MissileGoneOutOfBorder;
    [ProtoMember(13)] public readonly bool CollidedWithLastBoneSegment;
    [ProtoMember(14)] public readonly CombatCollisionResult CollisionResult;
    [ProtoMember(15)] public readonly int AffectorWeaponSlotOrMissileIndex;
    [ProtoMember(16)] public readonly int StrikeType;
    [ProtoMember(17)] public readonly int DamageType;
    [ProtoMember(18)] public readonly sbyte CollisionBoneIndex;
    [ProtoMember(19)] public readonly BoneBodyPartType VictimHitBodyPart;
    [ProtoMember(20)] public readonly sbyte AttackBoneIndex;
    [ProtoMember(21)] public readonly Agent.UsageDirection AttackDirection;
    [ProtoMember(22)] public readonly int PhysicsMaterialIndex;
    [ProtoMember(23)] public readonly CombatHitResultFlags CollisionHitResultFlags;
    [ProtoMember(24)] public readonly float AttackProgress;
    [ProtoMember(25)] public readonly float CollisionDistanceOnWeapon;
    [ProtoMember(26)] public readonly float AttackerStunPeriod;
    [ProtoMember(27)] public readonly float DefenderStunPeriod;
    [ProtoMember(28)] public readonly float MissileTotalDamage;
    [ProtoMember(29)] public readonly float MissileStartingBaseSpeed;
    [ProtoMember(30)] public readonly float ChargeVelocity;
    [ProtoMember(31)] public readonly float FallSpeed;
    [ProtoMember(32)] public readonly Vec3 WeaponRotUp;
    [ProtoMember(33)] public readonly Vec3 WeaponBlowDir;
    [ProtoMember(34)] public readonly Vec3 CollisionGlobalPosition;
    [ProtoMember(35)] public readonly Vec3 MissileVelocity;
    [ProtoMember(36)] public readonly Vec3 MissileStartingPosition;
    [ProtoMember(37)] public readonly Vec3 VictimAgentCurVelocity;
    [ProtoMember(38)] public readonly Vec3 CollisionGlobalNormal;
    [ProtoMember(39)] public readonly Vec3 LastBoneSegmentRotUp;
    [ProtoMember(40)] public readonly Vec3 LastBoneSegmentSwingDir;
    [ProtoMember(41)] public readonly float BaseMagnitude;
    [ProtoMember(42)] public readonly float MovementSpeedDamageModifier;
    [ProtoMember(43)] public readonly int AbsorbedByArmor;
    [ProtoMember(44)] public readonly int InflictedDamage;
    [ProtoMember(45)] public readonly int SelfInflictedDamage;
    [ProtoMember(46)] public readonly bool IsShieldBroken;
    [ProtoMember(47)] public readonly bool IsSneakAttack;

    public BattleCollisionData(AttackCollisionData collision)
    {
        AttackBlockedWithShield = collision.AttackBlockedWithShield;
        CorrectSideShieldBlock = collision.CorrectSideShieldBlock;
        IsAlternativeAttack = collision.IsAlternativeAttack;
        IsColliderAgent = collision.IsColliderAgent;
        CollidedWithShieldOnBack = collision.CollidedWithShieldOnBack;
        IsMissile = collision.IsMissile;
        MissileBlockedWithWeapon = collision.MissileBlockedWithWeapon;
        MissileHasPhysics = collision.MissileHasPhysics;
        EntityExists = collision.EntityExists;
        ThrustTipHit = collision.ThrustTipHit;
        MissileGoneUnderWater = collision.MissileGoneUnderWater;
        MissileGoneOutOfBorder = collision.MissileGoneOutOfBorder;
        CollidedWithLastBoneSegment = collision.CollidedWithLastBoneSegment;
        CollisionResult = collision.CollisionResult;
        AffectorWeaponSlotOrMissileIndex = collision.AffectorWeaponSlotOrMissileIndex;
        StrikeType = collision.StrikeType;
        DamageType = collision.DamageType;
        CollisionBoneIndex = collision.CollisionBoneIndex;
        VictimHitBodyPart = collision.VictimHitBodyPart;
        AttackBoneIndex = collision.AttackBoneIndex;
        AttackDirection = collision.AttackDirection;
        PhysicsMaterialIndex = collision.PhysicsMaterialIndex;
        CollisionHitResultFlags = collision.CollisionHitResultFlags;
        AttackProgress = collision.AttackProgress;
        CollisionDistanceOnWeapon = collision.CollisionDistanceOnWeapon;
        AttackerStunPeriod = collision.AttackerStunPeriod;
        DefenderStunPeriod = collision.DefenderStunPeriod;
        MissileTotalDamage = collision.MissileTotalDamage;
        MissileStartingBaseSpeed = collision.MissileStartingBaseSpeed;
        ChargeVelocity = collision.ChargeVelocity;
        FallSpeed = collision.FallSpeed;
        WeaponRotUp = collision.WeaponRotUp;
        WeaponBlowDir = collision.WeaponBlowDir;
        CollisionGlobalPosition = collision.CollisionGlobalPosition;
        MissileVelocity = collision.MissileVelocity;
        MissileStartingPosition = collision.MissileStartingPosition;
        VictimAgentCurVelocity = collision.VictimAgentCurVelocity;
        CollisionGlobalNormal = collision.CollisionGlobalNormal;
        LastBoneSegmentRotUp = collision.LastBoneSegmentRotUp;
        LastBoneSegmentSwingDir = collision.LastBoneSegmentSwingDir;
        BaseMagnitude = collision.BaseMagnitude;
        MovementSpeedDamageModifier = collision.MovementSpeedDamageModifier;
        AbsorbedByArmor = collision.AbsorbedByArmor;
        InflictedDamage = collision.InflictedDamage;
        SelfInflictedDamage = collision.SelfInflictedDamage;
        IsShieldBroken = collision.IsShieldBroken;
        IsSneakAttack = collision.IsSneakAttack;
    }
}
