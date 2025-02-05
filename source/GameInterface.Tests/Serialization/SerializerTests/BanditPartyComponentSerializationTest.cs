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
using Autofac;
using GameInterface.Tests.Bootstrap.Modules;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BanditPartyComponentSerializationTest
    {
        readonly IContainer container;
        public BanditPartyComponentSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void BanditPartyComponent_Serialize()
        {
            BanditPartyComponent item = (BanditPartyComponent)FormatterServices.GetUninitializedObject(typeof(BanditPartyComponent));

            var factory = container.Resolve<IBinaryPackageFactory>();
            BanditPartyComponentBinaryPackage package = new BanditPartyComponentBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void BanditPartyComponent_Full_Serialization()
        {
            Hideout hideout = (Hideout)FormatterServices.GetUninitializedObject(typeof(Hideout));

            // TODO make atomic to not interfere with other tests that use Hideout.All
            MBList<Hideout> allhideouts = Campaign.Current?._hideouts ?? new MBList<Hideout>();

            allhideouts.Add(hideout);

            Assert.NotNull(Campaign.Current);

            Campaign.Current!._hideouts = allhideouts;

            MobileParty mobileParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            PartyBase party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
            Clan clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

            mobileParty.StringId = "MyMobileParty";
            clan.StringId = "myClan";

            party.MobileParty = mobileParty;
            mobileParty.Party = party;
            mobileParty._actualClan = clan;

            BanditPartyComponent item = (BanditPartyComponent)FormatterServices.GetUninitializedObject(typeof(BanditPartyComponent));
            item.Hideout = hideout;
            item.IsBossParty = true;
            item.MobileParty = mobileParty;

            var factory = container.Resolve<IBinaryPackageFactory>();
            BanditPartyComponentBinaryPackage package = new BanditPartyComponentBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BanditPartyComponentBinaryPackage>(obj);

            BanditPartyComponentBinaryPackage returnedPackage = (BanditPartyComponentBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            BanditPartyComponent newBanditPartyComponent = returnedPackage.Unpack<BanditPartyComponent>(deserializeFactory);

            Assert.Equal(item.IsBossParty, newBanditPartyComponent.IsBossParty);
            Assert.NotNull(newBanditPartyComponent.Hideout);
        }
    }
}
