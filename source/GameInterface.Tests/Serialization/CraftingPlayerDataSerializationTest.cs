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
        ILifetimeScope container;
        public CraftingPlayerDataSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void CraftingPlayerData_Full_Serialization()
        {
            CraftingPlayerData craftingPlayerData = (CraftingPlayerData)FormatterServices.GetUninitializedObject(typeof(CraftingPlayerData));

            var factory = container.Resolve<IBinaryPackageFactory>();
            CraftingPlayerDataBinaryPackage package = new CraftingPlayerDataBinaryPackage(craftingPlayerData, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<CraftingPlayerDataBinaryPackage>(obj);

            CraftingPlayerDataBinaryPackage returnedPackage = (CraftingPlayerDataBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            CraftingPlayerData newCraftingPlayerData = returnedPackage.Unpack<CraftingPlayerData>(deserializeFactory);

            Assert.Equal(craftingPlayerData.PlayerOpenNewPartXpDictionary, newCraftingPlayerData.PlayerOpenNewPartXpDictionary);
            Assert.Equal(craftingPlayerData.PlayerOpenedPartsDictionary, newCraftingPlayerData.PlayerOpenedPartsDictionary);
            Assert.Equal(craftingPlayerData.PlayerCraftedItemsHistory, newCraftingPlayerData.PlayerCraftedItemsHistory);
        }
    }
}
