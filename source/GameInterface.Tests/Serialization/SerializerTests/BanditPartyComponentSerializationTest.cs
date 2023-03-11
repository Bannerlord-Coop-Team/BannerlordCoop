using GameInterface.Serialization.External;
using GameInterface.Serialization;
using System.Collections.Generic;
using Xunit;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.Library;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BanditPartyComponentSerializationTest
    {
        public BanditPartyComponentSerializationTest()
        {
            GameBootStrap.Initialize();
        }

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
        private static readonly PropertyInfo PartyComponent_MobileParty = typeof(PartyComponent).GetProperty(nameof(PartyComponent.MobileParty));
        private static readonly PropertyInfo BanditPartyComponent_Hideout = typeof(BanditPartyComponent).GetProperty(nameof(BanditPartyComponent.Hideout));
        private static readonly PropertyInfo BanditPartyComponent_IsBossParty = typeof(BanditPartyComponent).GetProperty(nameof(BanditPartyComponent.IsBossParty));
        private static readonly PropertyInfo PartyBase_MobileParty = typeof(PartyBase).GetProperty(nameof(PartyBase.MobileParty));
        private static readonly FieldInfo MobileParty_actualClan = typeof(MobileParty).GetField("_actualClan", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo MobileParty_Party = typeof(MobileParty).GetProperty(nameof(MobileParty.Party));

        [Fact]
        public void BanditPartyComponent_Full_Serialization()
        {
            Campaign_Current.SetValue(null, FormatterServices.GetUninitializedObject(typeof(Campaign)));

            Hideout hideout = (Hideout)FormatterServices.GetUninitializedObject(typeof(Hideout));
            var allhideouts = new MBList<Hideout>
            {
                hideout
            };

            Campaign_hideouts.SetValue(Campaign.Current, allhideouts);

            MobileParty mobileParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            PartyBase party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
            Clan clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

            mobileParty.StringId = "MyMobileParty";
            clan.StringId = "myClan";

            PartyBase_MobileParty.SetValue(party, mobileParty);
            MobileParty_Party.SetValue(mobileParty, party);
            MobileParty_actualClan.SetValue(mobileParty, clan);

            BanditPartyComponent item = (BanditPartyComponent)FormatterServices.GetUninitializedObject(typeof(BanditPartyComponent));
            BanditPartyComponent_Hideout.SetValue(item, hideout);
            BanditPartyComponent_IsBossParty.SetValue(item, true);
            PartyComponent_MobileParty.SetValue(item, mobileParty);

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
