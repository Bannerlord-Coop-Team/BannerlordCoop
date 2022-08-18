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
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using static TaleWorlds.CampaignSystem.Hero;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;

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

            var testModel = MakeHeroSerializable();

            Hero hero = new Hero();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(hero);
            Hero newHero = ser.Deserialize<Hero>(data);

            Assert.NotNull(newHero);

            harmony.UnpatchAll();
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

            generator.AssignSurrogate<TextObject, TextObjectSurrogate>();
            generator.AssignSurrogate<CharacterObject, CharacterObjectSurrogate>();
            generator.AssignSurrogate<Equipment, EquipmentSurrogate>();
            generator.AssignSurrogate<CampaignTime, CampaignTimeSurrogate>();
            generator.AssignSurrogate<CharacterTraits, CharacterTraitsSurrogate>();
            generator.AssignSurrogate<CharacterPerks, CharacterPerksSurrogate>();
            generator.AssignSurrogate<CharacterSkills, CharacterSkillsSurrogate>();
            generator.AssignSurrogate<CharacterAttributes, CharacterAttributesSurrogate>();
            generator.AssignSurrogate<IssueBase, IssueBaseSurrogate>();
            generator.AssignSurrogate<Clan, ClanSurrogate>();
            generator.AssignSurrogate<HeroLastSeenInformation, HeroLastSeenInformationSurrogate>();
            generator.AssignSurrogate<Settlement, SettlementSurrogate>();
            generator.AssignSurrogate<Town, TownSurrogate>();
            generator.AssignSurrogate<CultureObject, CultureObjectSurrogate>();
            generator.AssignSurrogate<MobileParty, MobilePartySurrogate>();
            generator.AssignSurrogate<PartyBase, PartyBaseSurrogate>();
            generator.AssignSurrogate<EquipmentElement, EquipmentElementSurrogate>();
            generator.AssignSurrogate<ItemObject, ItemObjectSurrogate>();
            generator.AssignSurrogate<ItemModifier, ItemModifierSurrogate>();
            generator.AssignSurrogate<HeroDeveloper, HeroDeveloperSurrogate>();

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
