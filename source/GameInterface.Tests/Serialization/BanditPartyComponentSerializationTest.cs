using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    [Collection(CampaignHideoutsTestCollection.Name)]
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

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void BanditPartyComponent_Full_Serialization()
        {
            var campaign = Campaign.Current;
            Assert.NotNull(campaign);
            var previousHideouts = campaign._hideouts;
            try
            {
                Hideout hideout = (Hideout)FormatterServices.GetUninitializedObject(typeof(Hideout));
                campaign._hideouts = new MBList<Hideout> { new Hideout(), hideout };

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

                byte[] bytes = BinaryPackageSerializer.Serialize(package);

                Assert.NotEmpty(bytes);

                object obj = BinaryPackageSerializer.Deserialize(bytes);

                Assert.IsType<BanditPartyComponentBinaryPackage>(obj);

                BanditPartyComponentBinaryPackage returnedPackage = (BanditPartyComponentBinaryPackage)obj;

                var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
                BanditPartyComponent newBanditPartyComponent = returnedPackage.Unpack<BanditPartyComponent>(deserializeFactory);

                Assert.Equal(item.IsBossParty, newBanditPartyComponent.IsBossParty);
                Assert.Same(hideout, newBanditPartyComponent.Hideout);
            }
            finally
            {
                campaign._hideouts = previousHideouts;
            }
        }
    }
}
