using GameInterface.Serialization.Dynamic;
using HarmonyLib;
using ProtoBuf.Meta;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.Hero;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class HeroObjectSerializationTests
    {
        private readonly ITestOutputHelper output;
        private readonly Harmony harmony;
        public HeroObjectSerializationTests(ITestOutputHelper output)
        {
            harmony = new Harmony($"testing.{GetType()}");
            harmony.PatchAll();
            this.output = output;
        }

        [Fact]
        public void NominalHeroObjectSerialization()
        {
            var testModel = MakeHeroSerializable();

            Hero hero = new Hero();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(hero);
            Hero newHero = ser.Deserialize<Hero>(data);

            Assert.NotNull(newHero);
        }

        [Fact]
        public void NullHeroObjectSerialization()
        {
            var testModel = MakeHeroSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);
            Hero newHero = ser.Deserialize<Hero>(data);

            Assert.Null(newHero);
        }

        private RuntimeTypeModel MakeHeroSerializable()
        {
            string[] excluded = new string[]
            {
                "_father",
                "_mother",
            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<Hero>(excluded);

            // Make interface serializable
            generator.CreateDynamicSerializer<IHeroDeveloper>();

            generator.AssignSurrogate<CharacterObject, SurrogateStub<CharacterObject>>();
            generator.AssignSurrogate<TextObject, SurrogateStub<TextObject>>();
            generator.AssignSurrogate<Equipment, SurrogateStub<Equipment>>();
            generator.AssignSurrogate<CampaignTime, SurrogateStub<CampaignTime>>();
            generator.AssignSurrogate<CharacterTraits, SurrogateStub<CharacterTraits>>();
            generator.AssignSurrogate<CharacterPerks, SurrogateStub<CharacterPerks>>();
            generator.AssignSurrogate<CharacterSkills, SurrogateStub<CharacterSkills>>();
            generator.AssignSurrogate<CharacterAttributes, SurrogateStub<CharacterAttributes>>();
            generator.AssignSurrogate<IssueBase, SurrogateStub<IssueBase>>();
            generator.AssignSurrogate<Clan, SurrogateStub<Clan>>();
            generator.AssignSurrogate<HeroLastSeenInformation, SurrogateStub<HeroLastSeenInformation>>();
            generator.AssignSurrogate<Settlement, SurrogateStub<Settlement>>();
            generator.AssignSurrogate<Town, SurrogateStub<Town>>();
            generator.AssignSurrogate<CultureObject, SurrogateStub<CultureObject>>();
            generator.AssignSurrogate<MobileParty, SurrogateStub<MobileParty>>();
            generator.AssignSurrogate<PartyBase, SurrogateStub<PartyBase>>();
            generator.AssignSurrogate<EquipmentElement, SurrogateStub<EquipmentElement>>();
            generator.AssignSurrogate<ItemObject, SurrogateStub<ItemObject>>();
            generator.AssignSurrogate<ItemModifier, SurrogateStub<ItemModifier>>();
            generator.AssignSurrogate<HeroDeveloper, SurrogateStub<HeroDeveloper>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(Hero)));

            return testModel;
        }
    }

    [HarmonyPatch(typeof(Hero))]
    class HeroConstructorPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(MethodType.Constructor)]
        public static bool Prefix()
        {
            return false;
        }
    }
}
