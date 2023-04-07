using GameInterface.Serialization;
using Xunit;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Serialization.External;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using GameInterface.Tests.Bootstrap;
using Autofac;
using GameInterface.Tests.Bootstrap.Modules;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemRosterSerializationTest
    {
        IContainer container;
        public ItemRosterSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void ItemRoster_Serialize()
        {
            ItemRoster itemRoster = new ItemRoster();

            var factory = container.Resolve<IBinaryPackageFactory>();
            ItemRosterBinaryPackage package = new ItemRosterBinaryPackage(itemRoster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
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

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemRosterBinaryPackage>(obj);

            ItemRosterBinaryPackage returnedPackage = (ItemRosterBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            ItemRoster newRoster = returnedPackage.Unpack<ItemRoster>(deserializeFactory);

            Assert.Equal(itemRoster.Count, newRoster.Count);
            Assert.Equal(newRoster.ToString(), itemRoster.ToString());
        }
    }
}
