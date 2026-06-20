using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemCategorySerializationTest
    {
        IContainer container;
        public ItemCategorySerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void ItemCategory_Serialize()
        {
            ItemCategory testItemCategory = (ItemCategory)FormatterServices.GetUninitializedObject(typeof(ItemCategory));

            var factory = container.Resolve<IBinaryPackageFactory>();
            ItemCategoryBinaryPackage package = new ItemCategoryBinaryPackage(testItemCategory, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void ItemCategory_Full_Serialization()
        {
            ItemCategory testItemCategory = (ItemCategory)FormatterServices.GetUninitializedObject(typeof(ItemCategory));

            var factory = container.Resolve<IBinaryPackageFactory>();
            ItemCategoryBinaryPackage package = new ItemCategoryBinaryPackage(testItemCategory, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemCategoryBinaryPackage>(obj);

            ItemCategoryBinaryPackage returnedPackage = (ItemCategoryBinaryPackage)obj;

            Assert.Equal(returnedPackage.stringId, package.stringId);
        }
    }
}
