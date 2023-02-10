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
            BlowWeaponRecord blowWeaoponRecord = new BlowWeaponRecord();
            blowWeaoponRecord.AffectorWeaponSlotOrMissileIndex = 1;
            blowWeaoponRecord.BoneNoToAttach = 2;
            blowWeaoponRecord.CurrentPosition = new TaleWorlds.Library.Vec3(7, 8, 9);
            blowWeaoponRecord.ItemFlags = TaleWorlds.Core.ItemFlags.CanBePickedUpFromCorpse;
            blowWeaoponRecord.WeaponClass = TaleWorlds.Core.WeaponClass.Boulder;
            blowWeaoponRecord.StartingPosition = new TaleWorlds.Library.Vec3(10, 11, 12);
            blowWeaoponRecord.Velocity = new TaleWorlds.Library.Vec3(13, 14, 15);
            blowWeaoponRecord.WeaponFlags = TaleWorlds.Core.WeaponFlags.AffectsAreaBig;
            blowWeaoponRecord.Weight = 0.5f;
            blow.WeaponRecord = blowWeaoponRecord;

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BlowBinaryPackage package = new BlowBinaryPackage(blow, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            var deserializedFactory = new BinaryPackageFactory();
            var blowBinaryPackage = BinaryFormatterSerializer.Deserialize<BlowBinaryPackage>(bytes);
            blowBinaryPackage.BinaryPackageFactory = deserializedFactory;

            Blow deserializedBlow = blowBinaryPackage.Unpack<Blow>();
            Assert.Equal(deserializedBlow.AbsorbedByArmor, blow.AbsorbedByArmor);
            Assert.Equal(deserializedBlow.AttackerStunPeriod, blow.AttackerStunPeriod);
            Assert.Equal(deserializedBlow.AttackType, blow.AttackType);
            Assert.Equal(deserializedBlow.BaseMagnitude, blow.BaseMagnitude);
            Assert.Equal(deserializedBlow.BlowFlag, blow.BlowFlag);
            Assert.Equal(deserializedBlow.BoneIndex, blow.BoneIndex);
            Assert.Equal(deserializedBlow.DamageCalculated, blow.DamageCalculated);
            Assert.Equal(deserializedBlow.DamagedPercentage, blow.DamagedPercentage);
            Assert.Equal(deserializedBlow.DamageType, blow.DamageType);
            Assert.Equal(deserializedBlow.DefenderStunPeriod, blow.DefenderStunPeriod);
            Assert.Equal(deserializedBlow.Direction, blow.Direction);
            Assert.Equal(deserializedBlow.InflictedDamage, blow.InflictedDamage);
            Assert.Equal(deserializedBlow.IsFallDamage, blow.IsFallDamage);
            Assert.Equal(deserializedBlow.MovementSpeedDamageModifier, blow.MovementSpeedDamageModifier);
            Assert.Equal(deserializedBlow.NoIgnore, blow.NoIgnore);
            Assert.Equal(deserializedBlow.Position, blow.Position);
            Assert.Equal(deserializedBlow.SelfInflictedDamage, blow.SelfInflictedDamage);
            Assert.Equal(deserializedBlow.StrikeType, blow.StrikeType);
            Assert.Equal(deserializedBlow.SwingDirection, blow.SwingDirection);
            Assert.Equal(deserializedBlow.VictimBodyPart, blow.VictimBodyPart);
            Assert.Equal(deserializedBlow.WeaponRecord.AffectorWeaponSlotOrMissileIndex, blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex);
            Assert.Equal(deserializedBlow.WeaponRecord.BoneNoToAttach, blow.WeaponRecord.BoneNoToAttach);
            Assert.Equal(deserializedBlow.WeaponRecord.CurrentPosition, blow.WeaponRecord.CurrentPosition);
            Assert.Equal(deserializedBlow.WeaponRecord.ItemFlags, blow.WeaponRecord.ItemFlags);
            Assert.Equal(deserializedBlow.WeaponRecord.WeaponClass, blow.WeaponRecord.WeaponClass);
            Assert.Equal(deserializedBlow.WeaponRecord.StartingPosition, blow.WeaponRecord.StartingPosition);
            Assert.Equal(deserializedBlow.WeaponRecord.Velocity, blow.WeaponRecord.Velocity);
            Assert.Equal(deserializedBlow.WeaponRecord.WeaponFlags, blow.WeaponRecord.WeaponFlags);
            Assert.Equal(deserializedBlow.WeaponRecord.Weight, blow.WeaponRecord.Weight);
        }
    }
}
