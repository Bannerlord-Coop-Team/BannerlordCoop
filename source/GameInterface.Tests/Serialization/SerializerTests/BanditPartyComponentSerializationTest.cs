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

        private static readonly PropertyInfo Campaign_Current = typeof(Campaign).GetProperty("Current");
        private static readonly FieldInfo Campaign_hideouts = typeof(Campaign).GetField("_hideouts", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly PropertyInfo BanditPartyComponent_Hideout = typeof(BanditPartyComponent).GetProperty(nameof(BanditPartyComponent.Hideout));
        private static readonly PropertyInfo BanditPartyComponent_IsBossParty = typeof(BanditPartyComponent).GetProperty(nameof(BanditPartyComponent.IsBossParty));

        [Fact]
        public void BanditPartyComponent_Full_Serialization()
        {
            Campaign_Current.SetValue(null, FormatterServices.GetUninitializedObject(typeof(Campaign)));
            List<Hideout> allhideouts = new List<Hideout>();
            Hideout hideout = new Hideout();
            allhideouts.Add(hideout);
            Campaign_hideouts.SetValue(Campaign.Current, allhideouts);
            BanditPartyComponent item = (BanditPartyComponent)FormatterServices.GetUninitializedObject(typeof(BanditPartyComponent));
            BanditPartyComponent_Hideout.SetValue(item, hideout);
            BanditPartyComponent_IsBossParty.SetValue(item, true);
            BinaryPackageFactory factory = new BinaryPackageFactory();
            BanditPartyComponentBinaryPackage package = new BanditPartyComponentBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BanditPartyComponentBinaryPackage>(obj);

            BanditPartyComponentBinaryPackage returnedPackage = (BanditPartyComponentBinaryPackage)obj;

            BanditPartyComponent newBanditPartyComponent = returnedPackage.Unpack<BanditPartyComponent>();

            Assert.Equal(item.IsBossParty, newBanditPartyComponent.IsBossParty);
            Assert.NotNull(newBanditPartyComponent.Hideout);
        }

    }
}
