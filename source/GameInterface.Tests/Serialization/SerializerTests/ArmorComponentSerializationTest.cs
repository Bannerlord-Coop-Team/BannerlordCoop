using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ArmorComponentSerializationTest
    {
        public ArmorComponentSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void ArmorComponent_Serialize()
        {
            ItemObject itemObject = MBObjectManager.Instance.CreateObject<ItemObject>();
            ArmorComponent ArmorComponent = new ArmorComponent(itemObject);

            foreach(var property in typeof(ArmorComponent).GetProperties())
            {
                property.SetRandom(ArmorComponent);
            }

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ArmorComponentBinaryPackage package = new ArmorComponentBinaryPackage(ArmorComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void ArmorComponent_Full_Serialization()
        {
            ItemObject itemObject = MBObjectManager.Instance.CreateObject<ItemObject>();
            ArmorComponent ArmorComponent = new ArmorComponent(itemObject);

            foreach (var property in typeof(ArmorComponent).GetProperties())
            {
                property.SetRandom(ArmorComponent);
            }

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ArmorComponentBinaryPackage package = new ArmorComponentBinaryPackage(ArmorComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ArmorComponentBinaryPackage>(obj);

            ArmorComponentBinaryPackage returnedPackage = (ArmorComponentBinaryPackage)obj;

            ArmorComponent newArmorComponent = returnedPackage.Unpack<ArmorComponent>();

            foreach (var property in typeof(ArmorComponent).GetProperties())
            {
                Assert.Equal(property.GetValue(ArmorComponent), property.GetValue(newArmorComponent));
            }
        }
    }
}
