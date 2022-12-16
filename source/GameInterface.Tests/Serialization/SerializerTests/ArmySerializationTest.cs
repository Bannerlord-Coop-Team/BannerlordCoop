using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

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

        [Fact]
        public void Army_Full_Serialization()
        {
            Army armyObject = (Army)FormatterServices.GetUninitializedObject(typeof(Army));
            Hero tempHero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            tempHero.PassedTimeAtHomeSettlement = 89;

            armyObject.Cohesion = 67;
            armyObject.ArmyOwner = tempHero;
            armyObject.GetType().GetProperty("Morale", BindingFlags.Instance | BindingFlags.Public).SetValue(armyObject, 5);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ArmyBinaryPackage package = new ArmyBinaryPackage(armyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ArmyBinaryPackage>(obj);

            ArmyBinaryPackage returnedPackage = (ArmyBinaryPackage)obj;

            Army newArmyObject = returnedPackage.Unpack<Army>();

            Assert.Equal(armyObject.Cohesion, newArmyObject.Cohesion);
            Assert.Equal(armyObject.ArmyOwner.PassedTimeAtHomeSettlement, newArmyObject.ArmyOwner.PassedTimeAtHomeSettlement);
            Assert.Equal(armyObject.Morale, newArmyObject.Morale);
        }
    }
}
