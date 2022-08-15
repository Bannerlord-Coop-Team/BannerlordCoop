using Coop.Serialization;
using GameInterface.Serialization.DynamicModel;
using GameInterface.Serialization.Models;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests
{
    public class DynamicModelGeneratorTests
    {
        private readonly ITestOutputHelper output;

        public DynamicModelGeneratorTests(ITestOutputHelper output)
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

            IDynamicModelGenerator generator = new DynamicModelGenerator();

            generator.CreateDynamicSerializer<ItemObject>(excluded);
            generator.CreateDynamicSerializer<ItemComponent>();
            generator.CreateDynamicSerializer<Vec3>();

            generator.AssignSurrogate<TextObject, TextObjectSurrogate>();
            generator.Compile();

            ItemObject itemObject = new ItemObject();
            typeof(ItemObject).GetProperty("Name")?.SetValue(itemObject, new TextObject("Serialized Name"));
            typeof(ItemObject).GetProperty("Weight")?.SetValue(itemObject, 64);
            typeof(ItemObject).GetProperty("Difficulty")?.SetValue(itemObject, 12);
            typeof(ItemObject).GetProperty("IsFood")?.SetValue(itemObject, false);
            
            ProtobufSerializer ser = new ProtobufSerializer();
            byte[] data = ser.Serialize(itemObject);
            ItemObject deserializedItemObject = ser.Deserialize<ItemObject>(data);

            Assert.Equal(itemObject, deserializedItemObject);
        }

        [Fact]
        public void SerializeCampaignTime()
        {
            var campaignTime = CampaignTime.Days(5988410);
            
            IDynamicModelGenerator generator = new DynamicModelGenerator();
            generator.AssignSurrogate<CampaignTime, CampaignTimeSurrogate>();
            
            ProtobufSerializer serializer = new ProtobufSerializer();
            byte[] data = serializer.Serialize(campaignTime);
            CampaignTime deserializedCampaignTime = serializer.Deserialize<CampaignTime>(data);
            
            Assert.Equal(campaignTime, deserializedCampaignTime);
        }
        
        [Fact]
        public void SerializeSettlement()
        { 
            var settlement = new Settlement();
            
            IDynamicModelGenerator generator = new DynamicModelGenerator();
            generator.AssignSurrogate<Settlement, SettlementSurrogate>();
            
            ProtobufSerializer serializer = new ProtobufSerializer();
            byte[] data = serializer.Serialize(settlement);
            var deserializedSettlement = serializer.Deserialize<Settlement>(data);
            
            Assert.Equal(settlement, deserializedSettlement);
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
            
            IDynamicModelGenerator generator = new DynamicModelGenerator();
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

            ProtobufSerializer serializer = new ProtobufSerializer();
            byte[] data = serializer.Serialize(hero);
            var deserializedHero = serializer.Deserialize<Hero>(data);
            
            Assert.Equal(hero, deserializedHero);
            harmony.UnpatchAll();*/
        }
    }
}