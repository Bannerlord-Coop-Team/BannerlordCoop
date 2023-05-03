using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using Common.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TroopRosterElementSerializationTest
    {
        IContainer container;
        public TroopRosterElementSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

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

            var factory = container.Resolve<IBinaryPackageFactory>();
            TroopRosterElementBinaryPackage package = new TroopRosterElementBinaryPackage(TroopRosterElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void TroopRosterElement_Full_Serialization()
        {
            CharacterObject character = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
            TroopRosterElement troopRosterElement = new TroopRosterElement(character)
            {
                Number = 5,
                Xp = 100,
                WoundedNumber = 11
            };

            var factory = container.Resolve<IBinaryPackageFactory>();
            TroopRosterElementBinaryPackage package = new TroopRosterElementBinaryPackage(troopRosterElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TroopRosterElementBinaryPackage>(obj);

            TroopRosterElementBinaryPackage returnedPackage = (TroopRosterElementBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            TroopRosterElement newTroopRosterElement = returnedPackage.Unpack<TroopRosterElement>(deserializeFactory);

            Assert.Equal(troopRosterElement.Number, newTroopRosterElement.Number);
            Assert.Equal(troopRosterElement.Xp, newTroopRosterElement.Xp);
            Assert.Equal(troopRosterElement.WoundedNumber, newTroopRosterElement.WoundedNumber);
        }
    }
}
