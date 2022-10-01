using GameInterface.Serialization.Dynamic;
using GameInterface.Serialization.Helper;
using GameInterface.Serialization.Surrogates;
using HarmonyLib;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.Hero;

namespace GameInterface.Tests.Serialization.Surrogates
{
    public class HeroSerializationTests
    {
        private readonly ITestOutputHelper output;
        private readonly Harmony harmony;
        public HeroSerializationTests(ITestOutputHelper output)
        {
            harmony = new Harmony($"testing.{GetType()}");
            harmony.PatchAll();
            this.output = output;
        }

        [Fact]
        public void NominalHeroObjectSerialization()
        {
            var testModel = MakeHeroSerializable();
            Assert.True(testModel.CanSerialize(typeof(Hero)));

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

            output.WriteLine(SurrogateClassGenerator.GenerateClass(typeof(CharacterObject)));
        }

        private RuntimeTypeModel MakeHeroSerializable()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<Hero, HeroSurrogate>();

            generator.AssignSurrogate<IHeroDeveloper, SurrogateStub<IHeroDeveloper>>();
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
            generator.AssignSurrogate<Workshop, SurrogateStub<Workshop>>();
            generator.AssignSurrogate<CommonAreaPartyComponent, SurrogateStub<CommonAreaPartyComponent>>();
            generator.AssignSurrogate<CaravanPartyComponent, SurrogateStub<CaravanPartyComponent>>();

            generator.Compile();

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
