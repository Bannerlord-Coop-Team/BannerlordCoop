using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class TradeAgreementDecisionSerializationTest
    {
        [Fact]
        public void SerializeTradeAgreementDecision()
        {
            TradeAgreementDecisionData tradeAgreementDecisionData = new TradeAgreementDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "TargetKingdom");
            KingdomDecisionData kingdomDecisionDerivedData = tradeAgreementDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is TradeAgreementDecisionData);
            TradeAgreementDecisionData deserializedObj = (TradeAgreementDecisionData)obj;
            Assert.Equal(tradeAgreementDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(tradeAgreementDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(tradeAgreementDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(tradeAgreementDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(tradeAgreementDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(tradeAgreementDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(tradeAgreementDecisionData.TargetKingdomId, deserializedObj.TargetKingdomId);
        }

        [Fact]
        public void TradeAgreementDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(TradeAgreementDecisionData).GetField("TargetKingdomField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            FieldInfo? fieldInfo2 = typeof(TradeAgreementDecisionData).GetField("TradeAgreementsCampaignBehaviorField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo2);
            object? obj = fieldInfo?.GetValue(null);
            Assert.NotNull(obj);
            object? obj2 = fieldInfo2?.GetValue(null);
            Assert.NotNull(obj2);
        }
    }
}
