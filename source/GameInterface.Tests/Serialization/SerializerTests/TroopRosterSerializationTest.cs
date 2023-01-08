using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TroopRosterSerializationTest
    {
        [Fact]
        public void TroopRoster_Serialize()
        {
            PartyBase partybase = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
            TroopRoster TroopRoster = new TroopRoster(partybase);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TroopRosterBinaryPackage package = new TroopRosterBinaryPackage(TroopRoster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }


        private readonly FieldInfo data = typeof(TroopRoster).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly FieldInfo _isInitialized = typeof(TroopRoster).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly FieldInfo _totalWoundedHeroes = typeof(TroopRoster).GetField("_totalWoundedHeroes", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly FieldInfo _totalWoundedRegulars = typeof(TroopRoster).GetField("_totalWoundedRegulars", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly FieldInfo _totalHeroes = typeof(TroopRoster).GetField("_totalHeroes", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly FieldInfo _totalRegulars = typeof(TroopRoster).GetField("_totalRegulars", BindingFlags.NonPublic | BindingFlags.Instance);
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
            data.SetValue(TroopRoster, elements);
            _isInitialized.SetValue(TroopRoster, true);
            _totalHeroes.SetValue(TroopRoster, 33);
            _totalRegulars.SetValue(TroopRoster, 31);
            _totalWoundedHeroes.SetValue(TroopRoster, 43);
            _totalWoundedRegulars.SetValue(TroopRoster, 12);


            BinaryPackageFactory factory = new BinaryPackageFactory();
            TroopRosterBinaryPackage package = new TroopRosterBinaryPackage(TroopRoster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TroopRosterBinaryPackage>(obj);

            TroopRosterBinaryPackage returnedPackage = (TroopRosterBinaryPackage)obj;

            TroopRoster newTroopRoster = returnedPackage.Unpack<TroopRoster>();

            Assert.Equal(TroopRoster.Count, newTroopRoster.Count);
            Assert.Equal(_isInitialized.GetValue(TroopRoster), _isInitialized.GetValue(newTroopRoster));
            Assert.Equal(_totalHeroes.GetValue(TroopRoster), _totalHeroes.GetValue(newTroopRoster));
            Assert.Equal(_totalRegulars.GetValue(TroopRoster), _totalRegulars.GetValue(newTroopRoster));
            Assert.Equal(_totalWoundedHeroes.GetValue(TroopRoster), _totalWoundedHeroes.GetValue(newTroopRoster));
            Assert.Equal(_totalWoundedRegulars.GetValue(TroopRoster), _totalWoundedRegulars.GetValue(newTroopRoster));
        }
    }
}
