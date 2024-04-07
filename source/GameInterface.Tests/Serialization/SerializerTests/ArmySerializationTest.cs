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
using Common.Serialization;

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
            armyObject.Cohesion = ReflectionExtensions.Random<float>();
            armyObject.Morale = ReflectionExtensions.Random<float>();

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
            Assert.NotNull(newArmyObject._tickEvent);
            Assert.NotNull(newArmyObject._hourlyTickEvent);
            Assert.Same(armyObject.ArmyOwner, newArmyObject.ArmyOwner);
        }
    }
}
