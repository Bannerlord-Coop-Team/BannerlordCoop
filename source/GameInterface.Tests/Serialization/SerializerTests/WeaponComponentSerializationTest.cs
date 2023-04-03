using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WeaponComponentSerializationTest
    {
        IContainer container;
        public WeaponComponentSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void WeaponComponent_Serialize()
        {
            WeaponComponent weaponComponent = new WeaponComponent(new ItemObject());

            var factory = container.Resolve<IBinaryPackageFactory>();
            WeaponComponentBinaryPackage package = new WeaponComponentBinaryPackage(weaponComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);
            
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void WeaponComponent_Full_Serialization()
        {
            WeaponComponent weaponComponent = new WeaponComponent(new ItemObject());

            var factory = container.Resolve<IBinaryPackageFactory>();
            WeaponComponentBinaryPackage package = new WeaponComponentBinaryPackage(weaponComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);
                
            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<WeaponComponentBinaryPackage>(obj);

            WeaponComponentBinaryPackage returnedPackage = (WeaponComponentBinaryPackage)obj;

            WeaponComponent newWeaponComponent = returnedPackage.Unpack<WeaponComponent>();

            Assert.Equal(weaponComponent.Weapons, newWeaponComponent.Weapons);
        }
    }
}
