using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using System.Reflection;
using System.Runtime.Serialization;
using Common.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TroopRosterSerializationTest
    {
        IContainer container;
        public TroopRosterSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void TroopRoster_Serialize()
        {
            PartyBase partybase = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
            TroopRoster TroopRoster = new TroopRoster(partybase);

            var factory = container.Resolve<IBinaryPackageFactory>();
            TroopRosterBinaryPackage package = new TroopRosterBinaryPackage(TroopRoster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void TroopRoster_Full_Serialization()
        {
            MobileParty party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            TroopRoster TroopRoster = new TroopRoster(party.Party);

            CharacterObject character1 = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
            CharacterObject character2 = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
            
            TroopRosterElement[] elements = new TroopRosterElement[]
            {
                new TroopRosterElement(character1)
                {
                    Number = 2,
                    Xp = 101,
                    WoundedNumber = 12
                },
                new TroopRosterElement(character2)
                {
                    Number = 5,
                    Xp = 100,
                    WoundedNumber = 11
                }
            };

            TroopRoster.IsPrisonRoster = true;
            TroopRoster.data = elements;
            TroopRoster._isInitialized = true;
            TroopRoster._totalHeroes = 33;
            TroopRoster._totalRegulars = 31;
            TroopRoster._totalWoundedHeroes = 43;
            TroopRoster._totalWoundedRegulars = 12;


            var factory = container.Resolve<IBinaryPackageFactory>();
            TroopRosterBinaryPackage package = new TroopRosterBinaryPackage(TroopRoster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TroopRosterBinaryPackage>(obj);

            TroopRosterBinaryPackage returnedPackage = (TroopRosterBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            TroopRoster newTroopRoster = returnedPackage.Unpack<TroopRoster>(deserializeFactory);

            Assert.Equal(TroopRoster.Count, newTroopRoster.Count);
            Assert.Equal(TroopRoster._isInitialized, newTroopRoster._isInitialized);
            Assert.Equal(TroopRoster._totalHeroes, newTroopRoster._totalHeroes);
            Assert.Equal(TroopRoster._totalRegulars, newTroopRoster._totalRegulars);
            Assert.Equal(TroopRoster._totalWoundedHeroes, newTroopRoster._totalWoundedHeroes);
            Assert.Equal(TroopRoster._totalWoundedRegulars, newTroopRoster._totalWoundedRegulars);
        }
    }
}
