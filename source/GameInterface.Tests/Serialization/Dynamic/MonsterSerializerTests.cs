using GameInterface.Serialization.Dynamic;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class MonsterSerializationTests : IDisposable
    {
        private readonly ITestOutputHelper output;
        public MonsterSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Dispose()
        {
        }

        [Fact]
        public void NominalMonsterObjectSerialization()
        {
            var testModel = MakeMonsterSerializable();

            Monster monster = new Monster();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(monster);

            Monster newMonster = ser.Deserialize<Monster>(data);

            Assert.NotNull(newMonster);
        }

        [Fact]
        public void NullMonsterObjectSerialization()
        {
            var testModel = MakeMonsterSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            Monster newMonster = ser.Deserialize<Monster>(data);

            Assert.Null(newMonster);
        }

        private RuntimeTypeModel MakeMonsterSerializable()
        {
            string[] exclude = new string[]
            {
                "_monsterMissionData",
            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<Monster>(exclude);

            generator.AssignSurrogate<Vec3, SurrogateStub<Vec3>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(Monster)));

            return testModel;
        }
    }
}
