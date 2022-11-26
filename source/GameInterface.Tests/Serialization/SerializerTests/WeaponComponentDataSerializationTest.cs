using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WeaponComponentDataSerializationTest
    {
        [Fact]
        public void WeaponComponentData_Serialize()
        {
            WeaponComponentData weapondComponentData = new WeaponComponentData(new ItemObject());

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WeaponComponentDataBinaryPackage package = new WeaponComponentDataBinaryPackage(weapondComponentData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void WeaponComponentData_Full_Serialization()
        {
            WeaponComponentData weapondComponentData = new WeaponComponentData(new ItemObject());

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WeaponComponentDataBinaryPackage package = new WeaponComponentDataBinaryPackage(weapondComponentData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<WeaponComponentDataBinaryPackage>(obj);

            WeaponComponentDataBinaryPackage returnedPackage = (WeaponComponentDataBinaryPackage)obj;

            WeaponComponentData newWeapondComponentData = returnedPackage.Unpack<WeaponComponentData>();

            Assert.Equal(weapondComponentData.WeaponBalance, newWeapondComponentData.WeaponBalance);
            Assert.Equal(weapondComponentData.SweetSpotReach, newWeapondComponentData.WeaponBalance);
            Assert.Equal(weapondComponentData.Accuracy, newWeapondComponentData.WeaponBalance);
            Assert.Equal(weapondComponentData.Frame, newWeapondComponentData.Frame);
            Assert.Equal(weapondComponentData.AmmoClass, newWeapondComponentData.AmmoClass);
        }
    }
}
