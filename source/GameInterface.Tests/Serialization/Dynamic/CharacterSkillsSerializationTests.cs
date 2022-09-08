using GameInterface.Serialization.Dynamic;
using HarmonyLib;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using ProtoBuf;
using GameInterface.Serialization.Surrogates;
using TaleWorlds.Core;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class CharacterSkillsObjectSerializationTests : IDisposable
    {
        private readonly ITestOutputHelper output;
        public CharacterSkillsObjectSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Dispose()
        {
        }

        [Fact]
        public void NominalCharacterSkillsObjectSerialization()
        {
            var testModel = MakeCharacterSkillsSerializable();

            CharacterSkills characterSkills = new CharacterSkills();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(characterSkills);

            CharacterSkills newCharacterSkills = ser.Deserialize<CharacterSkills>(data);

            Assert.NotNull(newCharacterSkills);
        }

        [Fact]
        public void NullCharacterSkillsObjectSerialization()
        {
            var testModel = MakeCharacterSkillsSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            CharacterSkills newCharacterSkills = ser.Deserialize<CharacterSkills>(data);

            Assert.Null(newCharacterSkills);
        }

        private RuntimeTypeModel MakeCharacterSkillsSerializable()
        {
            string[] excluded = new string[]
            {

            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<CharacterSkills>(excluded);

            generator.AssignSurrogate<SkillObject, SurrogateStub<SkillObject>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(CharacterSkills)));

            return testModel;
        }
    }
}
