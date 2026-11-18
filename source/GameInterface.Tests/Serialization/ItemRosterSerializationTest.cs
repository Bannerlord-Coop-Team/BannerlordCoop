using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemRosterSerializationTest
    {
        ILifetimeScope container;
        public ItemRosterSerializationTest()
        {
            GameBootStrap.Initialize();

            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void ItemRoster_Full_Serialization()
        {
            ItemObject itemobj = MBObjectManager.Instance.CreateObject<ItemObject>();
            ItemRoster itemRoster = new ItemRoster
            {
                new ItemRosterElement(new EquipmentElement(itemobj), 1)
            };
            var factory = container.Resolve<IBinaryPackageFactory>();
            ItemRosterBinaryPackage package = new ItemRosterBinaryPackage(itemRoster, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<ItemRosterBinaryPackage>(obj);

            ItemRosterBinaryPackage returnedPackage = (ItemRosterBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            ItemRoster newRoster = returnedPackage.Unpack<ItemRoster>(deserializeFactory);

            Assert.Equal(itemRoster.Count, newRoster.Count);
            Assert.Equal(newRoster.ToString(), itemRoster.ToString());
        }

        [Fact]
        public void ItemRoster_Unpack_RebuildsCachedFood()
        {
            ItemObject grain = MBObjectManager.Instance.CreateObject<ItemObject>();
            grain.IsFood = true;
            grain.Value = 10;
            ItemRoster itemRoster = new ItemRoster
            {
                new ItemRosterElement(new EquipmentElement(grain), 3)
            };
            var factory = container.Resolve<IBinaryPackageFactory>();
            ItemRosterBinaryPackage package = new ItemRosterBinaryPackage(itemRoster, factory);
            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);
            var returnedPackage = (ItemRosterBinaryPackage)BinaryPackageSerializer.Deserialize(bytes);
            ItemRoster newRoster = returnedPackage.Unpack<ItemRoster>(container.Resolve<IBinaryPackageFactory>());

            Assert.Equal(3, newRoster.TotalFood);
            Assert.Equal(1, newRoster.FoodVariety);
            Assert.Equal(30, newRoster.TotalValue);
        }
    }
}
