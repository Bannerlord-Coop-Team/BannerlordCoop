using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Surrogates;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for compact routed-damage engine struct encoding.</summary>
public class BattleDamageCodecTests
{
    [Fact]
    public void EncodeDecode_RoundTripsFullEngineStructDataUnderPacketBudget()
    {
        var blow = new Blow(17)
        {
            GlobalPosition = new Vec3(1f, 2f, 3f),
            Direction = new Vec3(4f, 5f, 6f),
            SwingDirection = new Vec3(7f, 8f, 9f),
            InflictedDamage = 41,
            SelfInflictedDamage = 2,
            BaseMagnitude = 13f,
            DefenderStunPeriod = 0.4f,
            AttackerStunPeriod = 0.5f,
            AbsorbedByArmor = 6f,
            MovementSpeedDamageModifier = 1.2f,
            StrikeType = StrikeType.Thrust,
            AttackType = AgentAttackType.Bash,
            BlowFlag = BlowFlags.CanDismount | BlowFlags.KnockBack,
            BoneIndex = 3,
            VictimBodyPart = BoneBodyPartType.ShoulderRight,
            DamageType = DamageTypes.Pierce,
            NoIgnore = true,
            DamageCalculated = true,
            DamagedPercentage = 0.75f,
        };
        blow.WeaponRecord = new BlowWeaponRecord
        {
            StartingPosition = new Vec3(10f, 11f, 12f),
            CurrentPosition = new Vec3(13f, 14f, 15f),
            Velocity = new Vec3(16f, 17f, 18f),
            ItemFlags = ItemFlags.Civilian,
            WeaponFlags = WeaponFlags.RangedWeapon,
            WeaponClass = WeaponClass.Arrow,
            BoneNoToAttach = 4,
            AffectorWeaponSlotOrMissileIndex = 42,
            Weight = 1.5f,
            _isMissile = true,
            _isMaterialMetal = true,
        };

        Vec3 weaponRotUp = new Vec3(1f, 2f, 3f);
        Vec3 weaponBlowDir = new Vec3(4f, 5f, 6f);
        Vec3 collisionPosition = new Vec3(7f, 8f, 9f);
        Vec3 missileVelocity = new Vec3(10f, 11f, 12f);
        Vec3 missileStartingPosition = new Vec3(13f, 14f, 15f);
        Vec3 victimVelocity = new Vec3(16f, 17f, 18f);
        Vec3 collisionNormal = new Vec3(19f, 20f, 21f);
        Vec3 lastBoneRotUp = new Vec3(22f, 23f, 24f);
        Vec3 lastBoneSwingDir = new Vec3(25f, 26f, 27f);
        var collisionData = new AttackCollisionData(
            true, false, true, false, true, false, true, false, true, false, true, false, true,
            CombatCollisionResult.StrikeAgent,
            42,
            (int)StrikeType.Thrust,
            (int)DamageTypes.Pierce,
            3,
            BoneBodyPartType.ShoulderRight,
            5,
            Agent.UsageDirection.AttackBegin,
            30,
            CombatHitResultFlags.HitWithArm,
            0.5f,
            0.8f,
            0.7f,
            1.1f,
            50f,
            20f,
            0.2f,
            0.3f,
            weaponRotUp,
            weaponBlowDir,
            collisionPosition,
            missileVelocity,
            missileStartingPosition,
            victimVelocity,
            collisionNormal,
            lastBoneRotUp,
            lastBoneSwingDir);
        collisionData.BaseMagnitude = 13f;
        collisionData.MovementSpeedDamageModifier = 1.2f;
        collisionData.AbsorbedByArmor = 6;
        collisionData.InflictedDamage = 41;
        collisionData.SelfInflictedDamage = 2;
        collisionData.IsShieldBroken = true;
        collisionData.IsSneakAttack = true;

        var codec = new BattleDamageCodec();
        BattleDamageData encoded = codec.Encode(in blow, in collisionData);

        new SurrogateCollection();
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        byte[] messagePayload = MessagePacket.Create(
            new NetworkApplyBattleDamage(
                Guid.NewGuid(),
                Guid.NewGuid(),
                encoded,
                isMissile: true),
            serializer).Data;
        Assert.InRange(messagePayload.Length, 1, 1199);
        Assert.True(codec.TryDecode(encoded, out Blow decodedBlow, out AttackCollisionData decodedCollision));
        BattleDamageData reencoded = codec.Encode(in decodedBlow, in decodedCollision);

        Assert.Equal(serializer.Serialize(encoded), serializer.Serialize(reencoded));
        Assert.Equal(blow.BlowFlag, decodedBlow.BlowFlag);
        Assert.Equal(blow.WeaponRecord.Velocity, decodedBlow.WeaponRecord.Velocity);
        Assert.True(decodedBlow.WeaponRecord.IsMissile);
        Assert.Equal(collisionData.AttackBlockedWithShield, decodedCollision.AttackBlockedWithShield);
        Assert.Equal(collisionData.CorrectSideShieldBlock, decodedCollision.CorrectSideShieldBlock);
        Assert.Equal(collisionData.IsAlternativeAttack, decodedCollision.IsAlternativeAttack);
        Assert.Equal(collisionData.IsColliderAgent, decodedCollision.IsColliderAgent);
        Assert.Equal(collisionData.CollidedWithShieldOnBack, decodedCollision.CollidedWithShieldOnBack);
        Assert.Equal(collisionData.IsMissile, decodedCollision.IsMissile);
        Assert.Equal(collisionData.MissileBlockedWithWeapon, decodedCollision.MissileBlockedWithWeapon);
        Assert.Equal(collisionData.MissileHasPhysics, decodedCollision.MissileHasPhysics);
        Assert.Equal(collisionData.EntityExists, decodedCollision.EntityExists);
        Assert.Equal(collisionData.ThrustTipHit, decodedCollision.ThrustTipHit);
        Assert.Equal(collisionData.MissileGoneUnderWater, decodedCollision.MissileGoneUnderWater);
        Assert.Equal(collisionData.MissileGoneOutOfBorder, decodedCollision.MissileGoneOutOfBorder);
        Assert.Equal(collisionData.CollidedWithLastBoneSegment, decodedCollision.CollidedWithLastBoneSegment);
        Assert.Equal(collisionData.CollisionResult, decodedCollision.CollisionResult);
        Assert.Equal(collisionData.AffectorWeaponSlotOrMissileIndex, decodedCollision.AffectorWeaponSlotOrMissileIndex);
        Assert.Equal(collisionData.StrikeType, decodedCollision.StrikeType);
        Assert.Equal(collisionData.DamageType, decodedCollision.DamageType);
        Assert.Equal(collisionData.CollisionBoneIndex, decodedCollision.CollisionBoneIndex);
        Assert.Equal(collisionData.VictimHitBodyPart, decodedCollision.VictimHitBodyPart);
        Assert.Equal(collisionData.AttackBoneIndex, decodedCollision.AttackBoneIndex);
        Assert.Equal(collisionData.AttackDirection, decodedCollision.AttackDirection);
        Assert.Equal(collisionData.PhysicsMaterialIndex, decodedCollision.PhysicsMaterialIndex);
        Assert.Equal(collisionData.CollisionHitResultFlags, decodedCollision.CollisionHitResultFlags);
        Assert.Equal(collisionData.AttackProgress, decodedCollision.AttackProgress);
        Assert.Equal(collisionData.CollisionDistanceOnWeapon, decodedCollision.CollisionDistanceOnWeapon);
        Assert.Equal(collisionData.AttackerStunPeriod, decodedCollision.AttackerStunPeriod);
        Assert.Equal(collisionData.DefenderStunPeriod, decodedCollision.DefenderStunPeriod);
        Assert.Equal(collisionData.MissileTotalDamage, decodedCollision.MissileTotalDamage);
        Assert.Equal(collisionData.MissileStartingBaseSpeed, decodedCollision.MissileStartingBaseSpeed);
        Assert.Equal(collisionData.ChargeVelocity, decodedCollision.ChargeVelocity);
        Assert.Equal(collisionData.FallSpeed, decodedCollision.FallSpeed);
        Assert.Equal(collisionData.WeaponRotUp, decodedCollision.WeaponRotUp);
        Assert.Equal(collisionData.WeaponBlowDir, decodedCollision.WeaponBlowDir);
        Assert.Equal(collisionData.CollisionGlobalPosition, decodedCollision.CollisionGlobalPosition);
        Assert.Equal(collisionData.MissileVelocity, decodedCollision.MissileVelocity);
        Assert.Equal(collisionData.MissileStartingPosition, decodedCollision.MissileStartingPosition);
        Assert.Equal(collisionData.VictimAgentCurVelocity, decodedCollision.VictimAgentCurVelocity);
        Assert.Equal(collisionData.CollisionGlobalNormal, decodedCollision.CollisionGlobalNormal);
        Assert.Equal(collisionData.LastBoneSegmentRotUp, decodedCollision.LastBoneSegmentRotUp);
        Assert.Equal(collisionData.LastBoneSegmentSwingDir, decodedCollision.LastBoneSegmentSwingDir);
        Assert.Equal(collisionData.BaseMagnitude, decodedCollision.BaseMagnitude);
        Assert.Equal(collisionData.MovementSpeedDamageModifier, decodedCollision.MovementSpeedDamageModifier);
        Assert.Equal(collisionData.AbsorbedByArmor, decodedCollision.AbsorbedByArmor);
        Assert.Equal(collisionData.InflictedDamage, decodedCollision.InflictedDamage);
        Assert.Equal(collisionData.SelfInflictedDamage, decodedCollision.SelfInflictedDamage);
        Assert.Equal(collisionData.IsShieldBroken, decodedCollision.IsShieldBroken);
        Assert.Equal(collisionData.IsSneakAttack, decodedCollision.IsSneakAttack);
    }

    [Fact]
    public void TryDecode_MissingOwnedData_ReturnsFalse()
    {
        var codec = new BattleDamageCodec();
        Blow blow = default;
        AttackCollisionData collisionData = default;
        BattleDamageData valid = codec.Encode(in blow, in collisionData);
        var invalid = new BattleDamageData(null, valid.Collision);

        Assert.False(codec.TryDecode(invalid, out _, out _));
    }
}
