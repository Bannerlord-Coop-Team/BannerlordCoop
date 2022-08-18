using Coop.Serialization;
using GameInterface.Serialization.Surrogates;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.Serialization.Dynamic;
using ProtoBuf.Meta;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using System;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Tests.Serialization
{
    public partial class SerializationTests
    {
        private readonly ITestOutputHelper output;

        public SerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void SerializeItemObject()
        {
            string[] excluded = new string[]
            {
                "<ItemCategory>k__BackingField",
                "<Culture>k__BackingField",
                "<WeaponDesign>k__BackingField",
            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<ItemObject>(excluded);
            generator.CreateDynamicSerializer<ItemComponent>();
            generator.CreateDynamicSerializer<Vec3>();

            generator.AssignSurrogate<TextObject, TextObjectSurrogate>();
            generator.Compile();

            Assert.True(testModel.CanSerialize(typeof(ItemObject)));

            ItemObject itemObject = new ItemObject();
            typeof(ItemObject).GetProperty("Name").SetValue(itemObject, new TextObject("Serialized Name"));
            typeof(ItemObject).GetProperty("Weight").SetValue(itemObject, 64);
            typeof(ItemObject).GetProperty("Difficulty").SetValue(itemObject, 12);
            typeof(ItemObject).GetProperty("IsFood").SetValue(itemObject, false);

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(itemObject);
            ItemObject deserializedItemObject = ser.Deserialize<ItemObject>(data);

            Assert.Equal(itemObject.Name.ToString(), deserializedItemObject.Name.ToString());
            Assert.Equal(itemObject.Weight, deserializedItemObject.Weight);
            Assert.Equal(itemObject.Difficulty, deserializedItemObject.Difficulty);
            Assert.Equal(itemObject.IsFood, deserializedItemObject.IsFood);
        }

        [Fact]
        public void SerializeCampaignTime()
        {
            var campaignTime = CampaignTime.Days(5988410);

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);
            generator.AssignSurrogate<CampaignTime, CampaignTimeSurrogate>();

            TestProtobufSerializer serializer = new TestProtobufSerializer(testModel);
            byte[] data = serializer.Serialize(campaignTime);
            CampaignTime deserializedCampaignTime = serializer.Deserialize<CampaignTime>(data);
            
            Assert.Equal(campaignTime, deserializedCampaignTime);
        }

        [Fact]
        public void SerializeHero()
        {
            /*
            var harmony = new Harmony("GameInterface.Tests");
            harmony.PatchAll();
            
            var hero = new Hero();

            var heroExcludedFields = new string[]
            {
                "_father",
                "_mother",
                "<Issue>k__BackingField",
                "_cachedLastSeenInformation",
                "_lastSeenInformationKnownToPlayer",
                "SpecialItems",
                "<BannerItem>k__BackingField",
                "<PartyBelongedToAsPrisoner>k__BackingField",
                "_heroDeveloper",
                "_governorOf"
            };

            var clanExcludedFields = new string[]
            {
                "_defaultPartyTemplate",
                "_banner",
                "OnPartiesAndLordsCacheUpdated"
            };
            
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);
            generator.CreateDynamicSerializer<Hero>(heroExcludedFields);
            generator.CreateDynamicSerializer<CharacterObject>();
            generator.CreateDynamicSerializer<Equipment>();
            generator.CreateDynamicSerializer<CharacterTraits>();
            generator.CreateDynamicSerializer<CharacterPerks>();
            generator.CreateDynamicSerializer<CharacterSkills>();
            generator.CreateDynamicSerializer<CharacterAttributes>();
            generator.CreateDynamicSerializer<Clan>(clanExcludedFields);
            generator.CreateDynamicSerializer<MBCharacterSkills>();
            generator.CreateDynamicSerializer<TraitObject>();
            generator.CreateDynamicSerializer<ItemCategory>();
            generator.CreateDynamicSerializer<Vec2>();
            
            generator.AssignSurrogate<TextObject, TextObjectSurrogate>();
            generator.AssignSurrogate<CampaignTime, CampaignTimeSurrogate>();
            generator.AssignSurrogate<CultureObject, CultureSurrogate>();
            generator.AssignSurrogate<Settlement, SettlementSurrogate>();
            generator.AssignSurrogate<MobileParty, MobilePartySurrogate>();
            generator.AssignSurrogate<Kingdom, KingdomSurrogate>();

            generator.Compile();

            TestProtobufSerializer serializer = new TestProtobufSerializer(testModel);
            byte[] data = serializer.Serialize(hero);
            var deserializedHero = serializer.Deserialize<Hero>(data);
            
            Assert.Equal(hero, deserializedHero);
            harmony.UnpatchAll();*/
        }
    }
}