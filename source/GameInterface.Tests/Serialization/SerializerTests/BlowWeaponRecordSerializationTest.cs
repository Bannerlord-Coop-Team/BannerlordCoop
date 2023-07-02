using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    /// <summary>
    /// Binary package for Blow Weapon recrod Serialization
    /// </summary>
    public class BlowWeaponRecordSerializationTest
    {
        IContainer container;
        public BlowWeaponRecordSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void BlowWeaponRecord_Serialize()
        {
            BlowWeaponRecord blowWeaponRecord = new BlowWeaponRecord();

            blowWeaponRecord.CurrentPosition = new TaleWorlds.Library.Vec3(1, 1, 1);
            var factory = container.Resolve<IBinaryPackageFactory>();
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
            var factory = container.Resolve<IBinaryPackageFactory>();
            BlowWeaponRecordBinaryPackage package = new BlowWeaponRecordBinaryPackage(blowWeaponRecord, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            var deseriliazedFactory = container.Resolve<IBinaryPackageFactory>();
            var deserialzedlowWeaponRecordBinaryPackage = BinaryFormatterSerializer.Deserialize<BlowWeaponRecordBinaryPackage>(bytes);
            deserialzedlowWeaponRecordBinaryPackage.BinaryPackageFactory = deseriliazedFactory;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            BlowWeaponRecord deserializedBlowWeaponRecord = deserialzedlowWeaponRecordBinaryPackage.Unpack<BlowWeaponRecord>(deserializeFactory);

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
