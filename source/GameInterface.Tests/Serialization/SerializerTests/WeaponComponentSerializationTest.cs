using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WeaponComponentSerializationTest
    {
        [Fact]
        public void WeaponComponent_Serialize()
        {
            WeaponComponent weaponComponent = new WeaponComponent(new ItemObject());

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WeaponComponentBinaryPackage package = new WeaponComponentBinaryPackage(weaponComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);
            
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void WeaponComponent_Full_Serialization()
        {
            WeaponComponent weaponComponent = new WeaponComponent(new ItemObject());

            BinaryPackageFactory factory = new BinaryPackageFactory();
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
