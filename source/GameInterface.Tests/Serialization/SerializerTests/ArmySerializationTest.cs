using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ArmySerializationTest
    {
        public ArmySerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void Army_Serialize()
        {
            Army armyObject = (Army)FormatterServices.GetUninitializedObject(typeof(Army));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ArmyBinaryPackage package = new ArmyBinaryPackage(armyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo Army_tickEvent = typeof(Army).GetField("_tickEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo Army_hourlyTickEvent = typeof(Army).GetField("_hourlyTickEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo Army_Cohesion = typeof(Army).GetProperty(nameof(Army.Cohesion));
        private static readonly PropertyInfo Army_Morale = typeof(Army).GetProperty(nameof(Army.Morale));
        [Fact]
        public void Army_Full_Serialization()
        {
            Army armyObject = (Army)FormatterServices.GetUninitializedObject(typeof(Army));
            // Assign non default values to armyObject
            Hero hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            hero.StringId = "myHero";

            MBObjectManager.Instance.RegisterObject(hero);

            armyObject.ArmyOwner = hero;
            Army_Cohesion.SetRandom(armyObject);
            Army_Morale.SetRandom(armyObject);

            // Setup serialization for armyObject
            BinaryPackageFactory factory = new BinaryPackageFactory();
            ArmyBinaryPackage package = new ArmyBinaryPackage(armyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ArmyBinaryPackage>(obj);

            ArmyBinaryPackage returnedPackage = (ArmyBinaryPackage)obj;

            Army newArmyObject = returnedPackage.Unpack<Army>();

            // Verify newArmyObject values
            Assert.Equal(armyObject.Cohesion, newArmyObject.Cohesion);
            Assert.Equal(armyObject.Morale, newArmyObject.Morale);
            Assert.NotNull(Army_tickEvent.GetValue(newArmyObject));
            Assert.NotNull(Army_hourlyTickEvent.GetValue(newArmyObject));
            Assert.Same(armyObject.ArmyOwner, newArmyObject.ArmyOwner);
        }
    }
}
