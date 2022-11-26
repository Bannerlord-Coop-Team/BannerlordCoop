using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MonsterSerializationTest
    {
        [Fact]
        public void Monster_Serialize()
        {
            Monster monster = new Monster();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MonsterBinaryPackage package = new MonsterBinaryPackage(monster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Monster_Full_Serialization()
        {
            Monster monster = new Monster();

            FieldInfo _monsterMissionData = typeof(Monster).GetField("_monsterMissionData", BindingFlags.Instance | BindingFlags.NonPublic);

            _monsterMissionData.SetValue(monster, new MonsterMissionData(monster));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MonsterBinaryPackage package = new MonsterBinaryPackage(monster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MonsterBinaryPackage>(obj);

            MonsterBinaryPackage returnedPackage = (MonsterBinaryPackage)obj;

            Monster newMonster = returnedPackage.Unpack<Monster>();

            foreach(FieldInfo field in typeof(Monster).GetAllInstanceFields(MonsterBinaryPackage.Excludes))
            {
                Assert.Equal(field.GetValue(monster), field.GetValue(newMonster));
            }

            Assert.NotEqual(_monsterMissionData.GetValue(monster), _monsterMissionData.GetValue(newMonster));
        }
    }
}
