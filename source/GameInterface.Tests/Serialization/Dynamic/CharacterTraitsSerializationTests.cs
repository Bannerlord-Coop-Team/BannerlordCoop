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

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class CharacterTraitsObjectSerializationTests : IDisposable
    {
        private readonly ITestOutputHelper output;
        public CharacterTraitsObjectSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Dispose()
        {
        }

        [Fact]
        public void NominalCharacterTraitsObjectSerialization()
        {
            var testModel = MakeCharacterTraitsSerializable();

            CharacterTraits characterTraits = new CharacterTraits();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(characterTraits);

            CharacterTraits newCharacterTraits = ser.Deserialize<CharacterTraits>(data);

            Assert.NotNull(newCharacterTraits);
        }

        [Fact]
        public void NullCharacterTraitsObjectSerialization()
        {
            var testModel = MakeCharacterTraitsSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            CharacterTraits newCharacterTraits = ser.Deserialize<CharacterTraits>(data);

            Assert.Null(newCharacterTraits);
        }

        private RuntimeTypeModel MakeCharacterTraitsSerializable()
        {
            string[] excluded = new string[]
            {

            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<CharacterTraits>(excluded);

            generator.AssignSurrogate<TraitObject, SurrogateStub<TraitObject>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(CharacterTraits)));

            return testModel;
        }
    }
}
