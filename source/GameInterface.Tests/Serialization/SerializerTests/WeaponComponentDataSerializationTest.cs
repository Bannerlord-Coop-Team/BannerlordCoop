using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.Core;
using Xunit;
using static TaleWorlds.Core.WeaponComponentData;
using TaleWorlds.Library;
using Autofac;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WeaponComponentDataSerializationTest
    {
        IContainer container;
        public WeaponComponentDataSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void WeaponComponentData_Serialize()
        {
            WeaponComponentData weapondComponentData = new WeaponComponentData(new ItemObject());

            var factory = container.Resolve<IBinaryPackageFactory>();
            WeaponComponentDataBinaryPackage package = new WeaponComponentDataBinaryPackage(weapondComponentData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void WeaponComponentData_Full_Serialization()
        {
            WeaponComponentData weapondComponentData = new WeaponComponentData(new ItemObject());

            weapondComponentData.Init("testName","Cu","slicy",new DamageTypes(),new DamageTypes(),9,57,45,34,12,54,23,78,34,"testname",11,56,new MatrixFrame(1,2,3,4,5,6,7,8,9,10,11,12),new WeaponClass(),70,71,72,73,75,new Vec3(1,2,3),new WeaponTiers(),4);

            var factory = container.Resolve<IBinaryPackageFactory>();
            WeaponComponentDataBinaryPackage package = new WeaponComponentDataBinaryPackage(weapondComponentData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<WeaponComponentDataBinaryPackage>(obj);

            WeaponComponentDataBinaryPackage returnedPackage = (WeaponComponentDataBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            WeaponComponentData newWeapondComponentData = returnedPackage.Unpack<WeaponComponentData>(deserializeFactory);

            Assert.Equal(weapondComponentData.WeaponBalance, newWeapondComponentData.WeaponBalance);
            Assert.Equal(weapondComponentData.SweetSpotReach, newWeapondComponentData.SweetSpotReach);
            Assert.Equal(weapondComponentData.Accuracy, newWeapondComponentData.Accuracy);
            Assert.Equal(weapondComponentData.Frame, newWeapondComponentData.Frame);
            Assert.Equal(weapondComponentData.AmmoClass, newWeapondComponentData.AmmoClass);
        }
    }
}
