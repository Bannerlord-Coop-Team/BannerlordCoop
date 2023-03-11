using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.Core;
using Xunit;
using System.Runtime.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WeaponDesignElementSerializationTest
    {
        [Fact]
        public void WeaponDesignElement_Serialize()
        {
            WeaponDesignElement weaponDesignElement = (WeaponDesignElement)FormatterServices.GetUninitializedObject(typeof(WeaponDesignElement));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WeaponDesignElementBinaryPackage package = new WeaponDesignElementBinaryPackage(weaponDesignElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void WeaponDesignElement_Full_Serialization()
        {
            WeaponDesignElement weaponDesignElement = (WeaponDesignElement)FormatterServices.GetUninitializedObject(typeof(WeaponDesignElement));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WeaponDesignElementBinaryPackage package = new WeaponDesignElementBinaryPackage(weaponDesignElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<WeaponDesignElementBinaryPackage>(obj);

            WeaponDesignElementBinaryPackage returnedPackage = (WeaponDesignElementBinaryPackage)obj;

            WeaponDesignElement newWeaponDesignElement = returnedPackage.Unpack<WeaponDesignElement>();

            Assert.Equal(weaponDesignElement.ScaleFactor, newWeaponDesignElement.ScaleFactor);
            Assert.Equal(weaponDesignElement.ScalePercentage, newWeaponDesignElement.ScalePercentage);
            Assert.Equal(weaponDesignElement.CraftingPiece, newWeaponDesignElement.CraftingPiece);

        }
    }
}
