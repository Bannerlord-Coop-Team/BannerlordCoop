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
        public void TestPack()
        {
            BlowWeaponRecord blowWR = new BlowWeaponRecord();

            blowWR.CurrentPosition = new TaleWorlds.Library.Vec3(1, 1, 1);
            BinaryPackageFactory factory = new BinaryPackageFactory();
            BlowWeaponRecordBinaryPackage package = new BlowWeaponRecordBinaryPackage(blowWR, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            var f = new BinaryPackageFactory();
            var bf = BinaryFormatterSerializer.Deserialize<BlowWeaponRecordBinaryPackage>(bytes);
            bf.BinaryPackageFactory = f;

            BlowWeaponRecord b = bf.Unpack<BlowWeaponRecord>();
            Assert.Equal(b.CurrentPosition.X, blowWR.CurrentPosition.X);
        }
    }
}
