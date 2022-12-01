using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using System.Reflection;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BanditPartyComponentSerializationTest
    {
        [Fact]
        public void BanditPartyComponent_Serialize()
        {
            BanditPartyComponent item = (BanditPartyComponent)FormatterServices.GetUninitializedObject(typeof(BanditPartyComponent));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BanditPartyComponentBinaryPackage package = new BanditPartyComponentBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void BanditPartyComponent_Full_Serialization()
        {
            typeof(Campaign).GetProperty("Current").SetValue(null, FormatterServices.GetUninitializedObject(typeof(Campaign)));
            List<Hideout> allhideouts = new List<Hideout>();
            Hideout hideout = new Hideout();
            allhideouts.Add(hideout);
            typeof(Campaign).GetField("_hideouts", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(Campaign.Current, allhideouts);
            BanditPartyComponent item = (BanditPartyComponent)FormatterServices.GetUninitializedObject(typeof(BanditPartyComponent));
            item.GetType().GetProperty(nameof(BanditPartyComponent.Hideout)).SetValue(item, hideout);
            item.GetType().GetProperty(nameof(BanditPartyComponent.IsBossParty)).SetValue(item, true);
            BinaryPackageFactory factory = new BinaryPackageFactory();
            BanditPartyComponentBinaryPackage package = new BanditPartyComponentBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BanditPartyComponentBinaryPackage>(obj);

            BanditPartyComponentBinaryPackage returnedPackage = (BanditPartyComponentBinaryPackage)obj;

            BanditPartyComponent newBanditPartyComponent = returnedPackage.Unpack<BanditPartyComponent>();

            Assert.True(item.IsBossParty == newBanditPartyComponent.IsBossParty == true && newBanditPartyComponent.Hideout != null);
        }

    }
}
