using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.Smithing;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CraftingPlayerDataSerializationTest
    {
        IContainer container;
        public CraftingPlayerDataSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void CraftingPlayerData_Serialize()
        {
            CraftingPlayerData craftingPlayerData = (CraftingPlayerData)FormatterServices.GetUninitializedObject(typeof(CraftingPlayerData));

            var factory = container.Resolve<IBinaryPackageFactory>();
            CraftingPlayerDataBinaryPackage package = new CraftingPlayerDataBinaryPackage(craftingPlayerData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CraftingPlayerData_Full_Serialization()
        {
            CraftingPlayerData craftingPlayerData = (CraftingPlayerData)FormatterServices.GetUninitializedObject(typeof(CraftingPlayerData));

            var factory = container.Resolve<IBinaryPackageFactory>();
            CraftingPlayerDataBinaryPackage package = new CraftingPlayerDataBinaryPackage(craftingPlayerData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CraftingPlayerDataBinaryPackage>(obj);

            CraftingPlayerDataBinaryPackage returnedPackage = (CraftingPlayerDataBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            CraftingPlayerData newCraftingPlayerData = returnedPackage.Unpack<CraftingPlayerData>(deserializeFactory);

            Assert.Equal(craftingPlayerData.HeroOpenNewPartXpDictionary, newCraftingPlayerData.HeroOpenNewPartXpDictionary);
            Assert.Equal(craftingPlayerData.HeroOpenedPartsDictionary, newCraftingPlayerData.HeroOpenedPartsDictionary);
            Assert.Equal(craftingPlayerData.HeroCraftingItemsHistory, newCraftingPlayerData.HeroCraftingItemsHistory);
        }
    }
}
