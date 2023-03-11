using GameInterface.Serialization;
using Xunit;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Serialization.External;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using GameInterface.Tests.Bootstrap;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemRosterSerializationTest
    {
        public ItemRosterSerializationTest() 
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void ItemRoster_Serialize()
        {
            ItemRoster itemRoster = new ItemRoster();

            BinaryPackageFactory factory = new BinaryPackageFactory();
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
            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemRosterBinaryPackage package = new ItemRosterBinaryPackage(itemRoster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemRosterBinaryPackage>(obj);

            ItemRosterBinaryPackage returnedPackage = (ItemRosterBinaryPackage)obj;

            ItemRoster newRoster = returnedPackage.Unpack<ItemRoster>();

            Assert.Equal(itemRoster.Count, newRoster.Count);
            Assert.Equal(newRoster.ToString(), itemRoster.ToString());
        }
    }
}
