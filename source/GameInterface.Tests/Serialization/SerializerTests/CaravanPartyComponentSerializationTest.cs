using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CaravanPartyComponentSerializationTest
    {
        public CaravanPartyComponentSerializationTest()
        {
            MBObjectManager.Init();
            MBObjectManager.Instance.RegisterType<Settlement>("Settlement", "Settlements", 4U, true, false);
            MBObjectManager.Instance.RegisterType<Hero>("Hero", "Heros", 5U, true, false);
            MBObjectManager.Instance.RegisterType<MobileParty>("MobileParty", "MobilePartys", 5U, true, false);
        }

        [Fact]
        public void CaravanPartyComponent_Serialize()
        {
            CaravanPartyComponent CaravanPartyComponent = (CaravanPartyComponent)FormatterServices.GetUninitializedObject(typeof(CaravanPartyComponent));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CaravanPartyComponentBinaryPackage package = new CaravanPartyComponentBinaryPackage(CaravanPartyComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly PropertyInfo Owner = typeof(CaravanPartyComponent).GetProperty(nameof(CaravanPartyComponent.Owner));
        private static readonly PropertyInfo MobileParty = typeof(PartyComponent).GetProperty(nameof(PartyComponent.MobileParty));
        private static readonly PropertyInfo Settlement = typeof(CaravanPartyComponent).GetProperty(nameof(CaravanPartyComponent.Settlement));
        private static readonly FieldInfo _leader = typeof(CaravanPartyComponent).GetField("_leader", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void CaravanPartyComponent_Full_Serialization()
        {
            CaravanPartyComponent CaravanPartyComponent = (CaravanPartyComponent)FormatterServices.GetUninitializedObject(typeof(CaravanPartyComponent));

            // Setup field classes
            Hero hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            MobileParty party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

            hero.StringId = "myHero";
            settlement.StringId = "mySettlement";
            party.StringId = "myParty";

            MBObjectManager.Instance.RegisterObject(hero);
            MBObjectManager.Instance.RegisterObject(settlement);
            MBObjectManager.Instance.RegisterObject(party);

            // Set field classes
            _leader.SetValue(CaravanPartyComponent, hero);
            Owner.SetValue(CaravanPartyComponent, hero);
            MobileParty.SetValue(CaravanPartyComponent, party);
            Settlement.SetValue(CaravanPartyComponent, settlement);

            // Setup package and dependencies
            BinaryPackageFactory factory = new BinaryPackageFactory();
            CaravanPartyComponentBinaryPackage package = new CaravanPartyComponentBinaryPackage(CaravanPartyComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CaravanPartyComponentBinaryPackage>(obj);

            CaravanPartyComponentBinaryPackage returnedPackage = (CaravanPartyComponentBinaryPackage)obj;

            CaravanPartyComponent newCaravanPartyComponent = returnedPackage.Unpack<CaravanPartyComponent>();

            Assert.Equal(CaravanPartyComponent.Leader, newCaravanPartyComponent.Leader);
            Assert.Equal(CaravanPartyComponent.Owner, newCaravanPartyComponent.Owner);
            Assert.Equal(CaravanPartyComponent.MobileParty, newCaravanPartyComponent.MobileParty);
            Assert.Equal(CaravanPartyComponent.Settlement, newCaravanPartyComponent.Settlement);
        }
    }
}
