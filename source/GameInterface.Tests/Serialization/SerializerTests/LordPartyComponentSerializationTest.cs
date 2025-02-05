using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class LordPartyComponentSerializationTest
    {
        IContainer container;
        public LordPartyComponentSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void LordPartyComponent_Serialize()
        {
            LordPartyComponent LordPartyComponent = (LordPartyComponent)FormatterServices.GetUninitializedObject(typeof(LordPartyComponent));

            var factory = container.Resolve<IBinaryPackageFactory>();
            LordPartyComponentBinaryPackage package = new LordPartyComponentBinaryPackage(LordPartyComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void LordPartyComponent_Full_Serialization()
        {
            var objectManager = container.Resolve<IObjectManager>();
            Hero hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            MobileParty mobileParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            PartyBase party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
            Clan clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

            hero.StringId = "myHero";
            mobileParty.StringId = "MyMobileParty";
            clan.StringId = "myClan";

            party.MobileParty = mobileParty;
            mobileParty.Party = party;
            mobileParty._actualClan = clan;

            objectManager.AddExisting(hero.StringId, hero);
            objectManager.AddExisting(mobileParty.StringId, mobileParty);
            objectManager.AddExisting(clan.StringId, clan);

            LordPartyComponent LordPartyComponent = (LordPartyComponent)FormatterServices.GetUninitializedObject(typeof(LordPartyComponent));

            LordPartyComponent._leader = hero;
            LordPartyComponent.Owner = hero;
            LordPartyComponent.MobileParty = mobileParty;

            var factory = container.Resolve<IBinaryPackageFactory>();
            LordPartyComponentBinaryPackage package = new LordPartyComponentBinaryPackage(LordPartyComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<LordPartyComponentBinaryPackage>(obj);

            LordPartyComponentBinaryPackage returnedPackage = (LordPartyComponentBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            LordPartyComponent newLordPartyComponent = returnedPackage.Unpack<LordPartyComponent>(deserializeFactory);

            Assert.Equal(LordPartyComponent._leader, newLordPartyComponent._leader);
            Assert.Equal(LordPartyComponent.Owner, newLordPartyComponent.Owner);
            Assert.Equal(LordPartyComponent.Party, newLordPartyComponent.Party);
        }
    }
}
