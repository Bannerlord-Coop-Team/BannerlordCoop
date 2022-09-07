using GameInterface.Serialization.Dynamic;
using HarmonyLib;
using ProtoBuf.Meta;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class CharacterObjectSerializationTests : IDisposable
    {
        private readonly ITestOutputHelper output;
        private readonly Harmony harmony;
        public CharacterObjectSerializationTests(ITestOutputHelper output)
        {
            harmony = new Harmony($"testing.{GetType()}");
            harmony.PatchAll();
            this.output = output;
        }

        public void Dispose()
        {
            harmony.UnpatchAll();
        }

        [Fact]
        public void NominalCharacterObjectSerialization()
        {
            var testModel = MakeCharacterObjectSerializable();

            CharacterObject characterObject = new CharacterObject();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(characterObject);
            CharacterObject newHero = ser.Deserialize<CharacterObject>(data);

            Assert.NotNull(newHero);
        }

        [Fact]
        public void NullCharacterObjectSerialization()
        {
            var testModel = MakeCharacterObjectSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);
            CharacterObject newHero = ser.Deserialize<CharacterObject>(data);

            Assert.Null(newHero);
        }

        private RuntimeTypeModel MakeCharacterObjectSerializable()
        {
            string[] excluded = new string[]
            {

            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<CharacterObject>(excluded);

            // Make interface serializable
            generator.CreateDynamicSerializer<IHeroDeveloper>();

            generator.AssignSurrogate<Hero, SurrogateStub<Hero>>();
            generator.AssignSurrogate<TraitObject, SurrogateStub<TraitObject>>();
            generator.AssignSurrogate<CharacterTraits, SurrogateStub<CharacterTraits>>();
            generator.AssignSurrogate<ItemCategory, SurrogateStub<ItemCategory>>();
            generator.AssignSurrogate<TextObject, SurrogateStub<TextObject>>();
            generator.AssignSurrogate<MBCharacterSkills, SurrogateStub<MBCharacterSkills>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(CharacterObject)));

            return testModel;
        }
    }
}
