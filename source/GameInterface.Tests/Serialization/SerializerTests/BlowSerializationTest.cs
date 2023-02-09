using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    /// <summary>
    /// Binary package for Blow Serialization
    /// </summary>
    public class BlowSerializationTest
    {
        [Fact]
        public void Blow_Serialize()
        {
            Blow blow = new Blow();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BlowBinaryPackage package = new BlowBinaryPackage(blow, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Blow_Full_Serialize()
        {
            Blow blow = new Blow();
            blow.AbsorbedByArmor = 0.5f;
            blow.AttackerStunPeriod = 0.3f;
            blow.AttackType = TaleWorlds.Core.AgentAttackType.Bash;
            blow.BaseMagnitude = 0.7f;
            blow.BlowFlag = BlowFlags.CrushThrough;
            blow.BoneIndex = 2;
            blow.DamageCalculated = true;
            blow.DamagedPercentage = 10;
            blow.DamageType = TaleWorlds.Core.DamageTypes.Pierce;
            blow.DefenderStunPeriod = 2;
            blow.Direction = new TaleWorlds.Library.Vec3(1, 2, 3);
            blow.InflictedDamage = 20;
            blow.IsFallDamage = false;
            blow.MovementSpeedDamageModifier = 0.3f;
            blow.NoIgnore = true;
            blow.OwnerId = 2;
            blow.Position = new TaleWorlds.Library.Vec3(1, 2, 3);
            blow.SelfInflictedDamage = 5;
            blow.StrikeType = TaleWorlds.Core.StrikeType.Swing;
            blow.SwingDirection = new TaleWorlds.Library.Vec3(5, 6, 7);
            blow.VictimBodyPart = BoneBodyPartType.Neck;
            BlowWeaponRecord bwr = new BlowWeaponRecord();
            bwr.AffectorWeaponSlotOrMissileIndex = 1;
            bwr.BoneNoToAttach = 2;
            bwr.CurrentPosition = new TaleWorlds.Library.Vec3(7, 8, 9);
            bwr.ItemFlags = TaleWorlds.Core.ItemFlags.CanBePickedUpFromCorpse;
            bwr.WeaponClass = TaleWorlds.Core.WeaponClass.Boulder;
            bwr.StartingPosition = new TaleWorlds.Library.Vec3(10, 11, 12);
            bwr.Velocity = new TaleWorlds.Library.Vec3(13, 14, 15);
            bwr.WeaponFlags = TaleWorlds.Core.WeaponFlags.AffectsAreaBig;
            bwr.Weight = 0.5f;
            blow.WeaponRecord = bwr;

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BlowBinaryPackage package = new BlowBinaryPackage(blow, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            var f = new BinaryPackageFactory();
            var bf = BinaryFormatterSerializer.Deserialize<BlowBinaryPackage>(bytes);
            bf.BinaryPackageFactory = f;

            Blow b = bf.Unpack<Blow>();
            Assert.Equal(b.AbsorbedByArmor, blow.AbsorbedByArmor);
            Assert.Equal(b.AttackerStunPeriod, blow.AttackerStunPeriod);
            Assert.Equal(b.AttackType, blow.AttackType);
            Assert.Equal(b.BaseMagnitude, blow.BaseMagnitude);
            Assert.Equal(b.BlowFlag, blow.BlowFlag);
            Assert.Equal(b.BoneIndex, blow.BoneIndex);
            Assert.Equal(b.DamageCalculated, blow.DamageCalculated);
            Assert.Equal(b.DamagedPercentage, blow.DamagedPercentage);
            Assert.Equal(b.DamageType, blow.DamageType);
            Assert.Equal(b.DefenderStunPeriod, blow.DefenderStunPeriod);
            Assert.Equal(b.Direction, blow.Direction);
            Assert.Equal(b.InflictedDamage, blow.InflictedDamage);
            Assert.Equal(b.IsFallDamage, blow.IsFallDamage);
            Assert.Equal(b.MovementSpeedDamageModifier, blow.MovementSpeedDamageModifier);
            Assert.Equal(b.NoIgnore, blow.NoIgnore);
            Assert.Equal(b.Position, blow.Position);
            Assert.Equal(b.SelfInflictedDamage, blow.SelfInflictedDamage);
            Assert.Equal(b.StrikeType, blow.StrikeType);
            Assert.Equal(b.SwingDirection, blow.SwingDirection);
            Assert.Equal(b.VictimBodyPart, blow.VictimBodyPart);
            Assert.Equal(b.WeaponRecord.AffectorWeaponSlotOrMissileIndex, blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex);
            Assert.Equal(b.WeaponRecord.BoneNoToAttach, blow.WeaponRecord.BoneNoToAttach);
            Assert.Equal(b.WeaponRecord.CurrentPosition, blow.WeaponRecord.CurrentPosition);
            Assert.Equal(b.WeaponRecord.ItemFlags, blow.WeaponRecord.ItemFlags);
            Assert.Equal(b.WeaponRecord.WeaponClass, blow.WeaponRecord.WeaponClass);
            Assert.Equal(b.WeaponRecord.StartingPosition, blow.WeaponRecord.StartingPosition);
            Assert.Equal(b.WeaponRecord.Velocity, blow.WeaponRecord.Velocity);
            Assert.Equal(b.WeaponRecord.WeaponFlags, blow.WeaponRecord.WeaponFlags);
            Assert.Equal(b.WeaponRecord.Weight, blow.WeaponRecord.Weight);
        }
    }
}
