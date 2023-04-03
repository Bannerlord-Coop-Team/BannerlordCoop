using Autofac;
using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ArmorComponentSerializationTest
    {
        IContainer container;
        public ArmorComponentSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
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

            var factory = container.Resolve<IBinaryPackageFactory>();
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

            var factory = container.Resolve<IBinaryPackageFactory>();
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
