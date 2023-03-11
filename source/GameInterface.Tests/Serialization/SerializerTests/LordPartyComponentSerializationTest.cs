using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class LordPartyComponentSerializationTest
    {
        public LordPartyComponentSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void LordPartyComponent_Serialize()
        {
            LordPartyComponent LordPartyComponent = (LordPartyComponent)FormatterServices.GetUninitializedObject(typeof(LordPartyComponent));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            LordPartyComponentBinaryPackage package = new LordPartyComponentBinaryPackage(LordPartyComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo LordPartyComponent_leader = typeof(LordPartyComponent).GetField("_leader", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo PartyComponent_MobileParty = typeof(PartyComponent).GetProperty(nameof(PartyComponent.MobileParty));
        private static readonly PropertyInfo LordPartyComponent_Owner = typeof(LordPartyComponent).GetProperty(nameof(LordPartyComponent.Owner));
        private static readonly PropertyInfo PartyBase_MobileParty = typeof(PartyBase).GetProperty(nameof(PartyBase.MobileParty));
        private static readonly FieldInfo MobileParty_actualClan = typeof(MobileParty).GetField("_actualClan", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo MobileParty_Party = typeof(MobileParty).GetProperty(nameof(MobileParty.Party));
        [Fact]
        public void LordPartyComponent_Full_Serialization()
        {
            Hero hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            MobileParty mobileParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            PartyBase party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
            Clan clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

            hero.StringId = "myHero";
            mobileParty.StringId = "MyMobileParty";
            clan.StringId = "myClan";

            PartyBase_MobileParty.SetValue(party, mobileParty);
            MobileParty_Party.SetValue(mobileParty, party);
            MobileParty_actualClan.SetValue(mobileParty, clan);

            MBObjectManager.Instance.RegisterObject(hero);
            MBObjectManager.Instance.RegisterObject(mobileParty);
            MBObjectManager.Instance.RegisterObject(clan);

            LordPartyComponent LordPartyComponent = (LordPartyComponent)FormatterServices.GetUninitializedObject(typeof(LordPartyComponent));

            LordPartyComponent_leader.SetValue(LordPartyComponent, hero);
            LordPartyComponent_Owner.SetValue(LordPartyComponent, hero);
            PartyComponent_MobileParty.SetValue(LordPartyComponent, mobileParty);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            LordPartyComponentBinaryPackage package = new LordPartyComponentBinaryPackage(LordPartyComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<LordPartyComponentBinaryPackage>(obj);

            LordPartyComponentBinaryPackage returnedPackage = (LordPartyComponentBinaryPackage)obj;

            LordPartyComponent newLordPartyComponent = returnedPackage.Unpack<LordPartyComponent>();

            Assert.Equal(LordPartyComponent_leader.GetValue(LordPartyComponent), LordPartyComponent_leader.GetValue(newLordPartyComponent));
            Assert.Equal(LordPartyComponent.Owner, newLordPartyComponent.Owner);
            Assert.Equal(LordPartyComponent.Party, newLordPartyComponent.Party);
        }
    }
}
