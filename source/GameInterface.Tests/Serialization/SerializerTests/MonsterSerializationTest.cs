using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MonsterSerializationTest
    {
        [Fact]
        public void Monster_Serialize()
        {
            Monster testMonster = (Monster)FormatterServices.GetUninitializedObject(typeof(Monster));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MonsterBinaryPackage package = new MonsterBinaryPackage(testMonster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Monster_Full_Serialization()
        {
            Monster testMonster = (Monster)FormatterServices.GetUninitializedObject(typeof(Monster));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MonsterBinaryPackage package = new MonsterBinaryPackage(testMonster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MonsterBinaryPackage>(obj);

            MonsterBinaryPackage returnedPackage = (MonsterBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
