using GameInterface.Serialization.Dynamic;
using GameInterface.Serialization.Surrogates;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using Xunit;
using TaleWorlds.CampaignSystem;
using HarmonyLib;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class HeroObjectSerializationTests
    {
        private readonly ITestOutputHelper output;

        public HeroObjectSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void NominalHeroObjectSerialization()
        {
            Harmony harmony = new Harmony($"testing.{GetType()}");
            harmony.PatchAll();

            string[] excluded = new string[]
            {
                "<ItemCategory>k__BackingField",
                "<Culture>k__BackingField",
                "<WeaponDesign>k__BackingField",
            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<Hero>(excluded);

            generator.AssignSurrogate<TextObject, TextObjectSurrogate>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(Hero)));

            Hero hero = new Hero();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(hero);
            Hero newHero = ser.Deserialize<Hero>(data);

            Assert.NotNull(newHero);

            harmony.UnpatchAll();
        }
    }

    [HarmonyPatch(typeof(Hero))]
    class HeroConstructorPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(MethodType.Constructor)]
        public bool Prefix()
        {
            return false;
        }
    }
}
