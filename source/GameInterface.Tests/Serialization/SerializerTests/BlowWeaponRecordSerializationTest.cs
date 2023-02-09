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
            BlowWeaponRecord blowWR = new BlowWeaponRecord();

            blowWR.CurrentPosition = new TaleWorlds.Library.Vec3(1, 1, 1);
            BinaryPackageFactory factory = new BinaryPackageFactory();
            BlowWeaponRecordBinaryPackage package = new BlowWeaponRecordBinaryPackage(blowWR, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }
        [Fact]
        public void BlowWeaponRecord_Full_Serialize()
        {

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
            BinaryPackageFactory factory = new BinaryPackageFactory();
            BlowWeaponRecordBinaryPackage package = new BlowWeaponRecordBinaryPackage(bwr, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            var f = new BinaryPackageFactory();
            var bf = BinaryFormatterSerializer.Deserialize<BlowWeaponRecordBinaryPackage>(bytes);
            bf.BinaryPackageFactory = f;

            BlowWeaponRecord b = bf.Unpack<BlowWeaponRecord>();
            Assert.Equal(b.AffectorWeaponSlotOrMissileIndex, bwr.AffectorWeaponSlotOrMissileIndex);
            Assert.Equal(b.BoneNoToAttach, bwr.BoneNoToAttach);
            Assert.Equal(b.CurrentPosition, bwr.CurrentPosition);
            Assert.Equal(b.ItemFlags, bwr.ItemFlags);
            Assert.Equal(b.WeaponClass, bwr.WeaponClass);
            Assert.Equal(b.StartingPosition, bwr.StartingPosition);
            Assert.Equal(b.Velocity, bwr.Velocity);
            Assert.Equal(b.WeaponFlags, bwr.WeaponFlags);
            Assert.Equal(b.Weight, bwr.Weight);
        }
    }
}
