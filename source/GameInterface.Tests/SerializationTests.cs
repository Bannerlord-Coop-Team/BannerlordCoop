using Coop.Serialization;
using GameInterface.Serialization.DynamicModel;
using GameInterface.Serialization.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
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
    }
}