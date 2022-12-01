using GameInterface.Serialization.Native;
using GameInterface.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Serialization.Impl;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemRosterSerializerTest
    {
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
            ItemRoster itemRoster = new ItemRoster();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemRosterBinaryPackage package = new ItemRosterBinaryPackage(itemRoster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemRosterBinaryPackage>(obj);

            ItemRosterBinaryPackage returnedPackage = (ItemRosterBinaryPackage)obj;

            ItemRoster newRoster = returnedPackage.Unpack<ItemRoster>();

            //Assert Stuff
            Assert.Equal(itemRoster, newRoster);
        }
    }
}
