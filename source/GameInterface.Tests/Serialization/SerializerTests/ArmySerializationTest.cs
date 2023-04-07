using Autofac;
using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ArmySerializationTest
    {
        IContainer container;
        public ArmySerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Army_Serialize()
        {
            Army armyObject = (Army)FormatterServices.GetUninitializedObject(typeof(Army));

            var factory = container.Resolve<IBinaryPackageFactory>();
            ArmyBinaryPackage package = new ArmyBinaryPackage(armyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo Army_tickEvent = typeof(Army).GetField("_tickEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo Army_hourlyTickEvent = typeof(Army).GetField("_hourlyTickEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo Army_Cohesion = typeof(Army).GetProperty(nameof(Army.Cohesion));
        private static readonly PropertyInfo Army_Morale = typeof(Army).GetProperty(nameof(Army.Morale));
        private static readonly PropertyInfo Campaing_MapTimeTracker = typeof(Campaign).GetProperty("MapTimeTracker", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void Army_Full_Serialization()
        {
            Army armyObject = (Army)FormatterServices.GetUninitializedObject(typeof(Army));
            // Assign non default values to armyObject
            Hero hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            hero.StringId = "myHero";

            var objectManager = container.Resolve<IObjectManager>();
            Assert.True(objectManager.AddExisting(hero.StringId, hero));

            armyObject.ArmyOwner = hero;
            Army_Cohesion.SetRandom(armyObject);
            Army_Morale.SetRandom(armyObject);

            // Setup serialization for armyObject
            var factory = container.Resolve<IBinaryPackageFactory>();
            ArmyBinaryPackage package = new ArmyBinaryPackage(armyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ArmyBinaryPackage>(obj);

            ArmyBinaryPackage returnedPackage = (ArmyBinaryPackage)obj;
            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();

            Army newArmyObject = returnedPackage.Unpack<Army>(deserializeFactory);

            // Verify newArmyObject values
            Assert.Equal(armyObject.Cohesion, newArmyObject.Cohesion);
            Assert.Equal(armyObject.Morale, newArmyObject.Morale);
            Assert.NotNull(Army_tickEvent.GetValue(newArmyObject));
            Assert.NotNull(Army_hourlyTickEvent.GetValue(newArmyObject));
            Assert.Same(armyObject.ArmyOwner, newArmyObject.ArmyOwner);
        }
    }
}
