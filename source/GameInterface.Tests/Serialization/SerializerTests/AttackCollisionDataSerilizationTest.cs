using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class AttackCollisionDataSerilizationTest
    {
        [Fact]
        public void AttackCollisionData_Serialize()
        {
            AttackCollisionData attackCollisionData = new AttackCollisionData();
            
            BinaryPackageFactory factory = new BinaryPackageFactory();
            AttackCollisionDataBinaryPackage package = new AttackCollisionDataBinaryPackage(attackCollisionData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        } 
        [Fact]
        public void AttackCollisionData_Full_Serialize()
        {
            Vec3 randomLocation= new Vec3();
            AttackCollisionData acd = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(true, true, true, true,
                true, true, true, true, true, true, true, true, CombatCollisionResult.StrikeAgent, 10, 100, 20, 3, BoneBodyPartType.ShoulderRight, 5, Agent.UsageDirection.AttackBegin
                , 30, CombatHitResultFlags.HitWithArm, 0.5f, 0.8f, 0.7f, 1.1f, 50.0f, 20.0f, 0.2f, 
                0.3f, randomLocation, randomLocation, randomLocation, randomLocation, randomLocation, randomLocation, randomLocation);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            AttackCollisionDataBinaryPackage package = new AttackCollisionDataBinaryPackage(acd, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            var deserilizationFactory = new BinaryPackageFactory();
            var bf = BinaryFormatterSerializer.Deserialize<AttackCollisionDataBinaryPackage>(bytes);
            bf.BinaryPackageFactory = deserilizationFactory;


            AttackCollisionData b = bf.Unpack<AttackCollisionData>();
            Assert.Equal(b.AttackBlockedWithShield, acd.AttackBlockedWithShield);
            Assert.Equal(b.CorrectSideShieldBlock, acd.CorrectSideShieldBlock);
            Assert.Equal(b.IsAlternativeAttack, acd.IsAlternativeAttack);
            Assert.Equal(b.IsColliderAgent, acd.IsColliderAgent);
            Assert.Equal(b.CollidedWithShieldOnBack, acd.CollidedWithShieldOnBack);
            Assert.Equal(b.IsMissile, acd.IsMissile);
            Assert.Equal(b.MissileHasPhysics, acd.MissileHasPhysics);
            Assert.Equal(b.EntityExists, acd.EntityExists);
            Assert.Equal(b.ThrustTipHit, acd.ThrustTipHit);
            Assert.Equal(b.MissileGoneUnderWater, acd.MissileGoneUnderWater);
            Assert.Equal(b.MissileGoneOutOfBorder, acd.MissileGoneOutOfBorder);
            Assert.Equal(b.CollisionBoneIndex, acd.CollisionBoneIndex);
            Assert.Equal(b.CollisionResult, acd.CollisionResult);
            Assert.Equal(b.AffectorWeaponSlotOrMissileIndex, acd.AffectorWeaponSlotOrMissileIndex);
            Assert.Equal(b.StrikeType, acd.StrikeType);
            Assert.Equal(b.DamageType, acd.DamageType);
            Assert.Equal(b.CollisionBoneIndex, acd.CollisionBoneIndex);
            Assert.Equal(b.VictimHitBodyPart, acd.VictimHitBodyPart);
            Assert.Equal(b.AttackBoneIndex, acd.AttackBoneIndex);
            Assert.Equal(b.AttackDirection, acd.AttackDirection);
            Assert.Equal(b.PhysicsMaterialIndex, acd.PhysicsMaterialIndex);
            Assert.Equal(b.CollisionHitResultFlags, acd.CollisionHitResultFlags);
            Assert.Equal(b.AttackProgress, acd.AttackProgress);
            Assert.Equal(b.CollisionDistanceOnWeapon, acd.CollisionDistanceOnWeapon);
            Assert.Equal(b.AttackerStunPeriod, acd.AttackerStunPeriod);
            Assert.Equal(b.DefenderStunPeriod, acd.DefenderStunPeriod);
            Assert.Equal(b.MissileTotalDamage, acd.MissileTotalDamage);
            Assert.Equal(b.MissileStartingBaseSpeed, acd.MissileStartingBaseSpeed);
            Assert.Equal(b.ChargeVelocity, acd.ChargeVelocity);
            Assert.Equal(b.FallSpeed, acd.FallSpeed);
            Assert.Equal(b.WeaponRotUp, acd.WeaponRotUp);
            Assert.Equal(b.WeaponBlowDir, acd.WeaponBlowDir);
            Assert.Equal(b.CollisionGlobalPosition, acd.CollisionGlobalPosition);
            Assert.Equal(b.MissileVelocity, acd.MissileVelocity);
            Assert.Equal(b.MissileStartingPosition, acd.MissileStartingPosition);
            Assert.Equal(b.VictimAgentCurVelocity, acd.VictimAgentCurVelocity);
            Assert.Equal(b.CollisionGlobalNormal, acd.CollisionGlobalNormal);
        }
    }

   
}
