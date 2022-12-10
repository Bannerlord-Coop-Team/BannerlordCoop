using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ArmorComponentSerializationTest
    {
        private readonly ITestOutputHelper output;

        public ArmorComponentSerializationTest(ITestOutputHelper output)
        {
            this.output = output;
            MBObjectManager.Init();
            MBObjectManager.Instance.RegisterType<ItemObject>("Item", "Items", 4U, true, false);
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
