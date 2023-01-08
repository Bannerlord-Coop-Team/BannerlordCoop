using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TroopRosterElementSerializationTest
    {
        [Fact]
        public void TroopRosterElement_Serialize()
        {
            CharacterObject character = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
            TroopRosterElement TroopRosterElement = new TroopRosterElement(character)
            {
                Number = 5,
                Xp = 100,
                WoundedNumber = 11
            };

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TroopRosterElementBinaryPackage package = new TroopRosterElementBinaryPackage(TroopRosterElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void TroopRosterElement_Full_Serialization()
        {
            CharacterObject character = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
            TroopRosterElement TroopRosterElement = new TroopRosterElement(character)
            {
                Number = 5,
                Xp = 100,
                WoundedNumber = 11
            };

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TroopRosterElementBinaryPackage package = new TroopRosterElementBinaryPackage(TroopRosterElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TroopRosterElementBinaryPackage>(obj);

            TroopRosterElementBinaryPackage returnedPackage = (TroopRosterElementBinaryPackage)obj;

            TroopRosterElement newTroopRosterElement = returnedPackage.Unpack<TroopRosterElement>();

            Assert.Equal(TroopRosterElement.Number, newTroopRosterElement.Number);
            Assert.Equal(TroopRosterElement.Xp, newTroopRosterElement.Xp);
            Assert.Equal(TroopRosterElement.WoundedNumber, newTroopRosterElement.WoundedNumber);
        }
    }
}
