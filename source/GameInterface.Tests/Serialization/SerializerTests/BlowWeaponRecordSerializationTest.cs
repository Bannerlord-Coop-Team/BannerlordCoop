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
    /// Binary package for Blow Weapon recrod Serialization
    /// </summary>
    public class BlowWeaponRecordSerializationTest
    {
        [Fact]
        public void BlowWeaponRecord_Serialize()
        {
            BlowWeaponRecord blowWeaponRecord = new BlowWeaponRecord();

            blowWeaponRecord.CurrentPosition = new TaleWorlds.Library.Vec3(1, 1, 1);
            BinaryPackageFactory factory = new BinaryPackageFactory();
            BlowWeaponRecordBinaryPackage package = new BlowWeaponRecordBinaryPackage(blowWeaponRecord, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }
        [Fact]
        public void BlowWeaponRecord_Full_Serialize()
        {

            BlowWeaponRecord blowWeaponRecord = new BlowWeaponRecord();
            blowWeaponRecord.AffectorWeaponSlotOrMissileIndex = 1;
            blowWeaponRecord.BoneNoToAttach = 2;
            blowWeaponRecord.CurrentPosition = new TaleWorlds.Library.Vec3(7, 8, 9);
            blowWeaponRecord.ItemFlags = TaleWorlds.Core.ItemFlags.CanBePickedUpFromCorpse;
            blowWeaponRecord.WeaponClass = TaleWorlds.Core.WeaponClass.Boulder;
            blowWeaponRecord.StartingPosition = new TaleWorlds.Library.Vec3(10, 11, 12);
            blowWeaponRecord.Velocity = new TaleWorlds.Library.Vec3(13, 14, 15);
            blowWeaponRecord.WeaponFlags = TaleWorlds.Core.WeaponFlags.AffectsAreaBig;
            blowWeaponRecord.Weight = 0.5f;
            BinaryPackageFactory factory = new BinaryPackageFactory();
            BlowWeaponRecordBinaryPackage package = new BlowWeaponRecordBinaryPackage(blowWeaponRecord, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            var deseriliazedFactory = new BinaryPackageFactory();
            var deserialzedlowWeaponRecordBinaryPackage = BinaryFormatterSerializer.Deserialize<BlowWeaponRecordBinaryPackage>(bytes);
            deserialzedlowWeaponRecordBinaryPackage.BinaryPackageFactory = deseriliazedFactory;

            BlowWeaponRecord deserializedBlowWeaponRecord = deserialzedlowWeaponRecordBinaryPackage.Unpack<BlowWeaponRecord>();
            Assert.Equal(deserializedBlowWeaponRecord.AffectorWeaponSlotOrMissileIndex, blowWeaponRecord.AffectorWeaponSlotOrMissileIndex);
            Assert.Equal(deserializedBlowWeaponRecord.BoneNoToAttach, blowWeaponRecord.BoneNoToAttach);
            Assert.Equal(deserializedBlowWeaponRecord.CurrentPosition, blowWeaponRecord.CurrentPosition);
            Assert.Equal(deserializedBlowWeaponRecord.ItemFlags, blowWeaponRecord.ItemFlags);
            Assert.Equal(deserializedBlowWeaponRecord.WeaponClass, blowWeaponRecord.WeaponClass);
            Assert.Equal(deserializedBlowWeaponRecord.StartingPosition, blowWeaponRecord.StartingPosition);
            Assert.Equal(deserializedBlowWeaponRecord.Velocity, blowWeaponRecord.Velocity);
            Assert.Equal(deserializedBlowWeaponRecord.WeaponFlags, blowWeaponRecord.WeaponFlags);
            Assert.Equal(deserializedBlowWeaponRecord.Weight, blowWeaponRecord.Weight);
        }
    }
}
