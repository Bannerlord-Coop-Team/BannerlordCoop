using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class SettlementClaimantDecisionSerializationTest
    {
        [Fact]
        public void SerializeSettlementClaimantDecision()
        {
            SettlementClaimantDecisionData settlementClaimantDecisionData = new SettlementClaimantDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "Settlement1","Hero1", "Clan1");
            KingdomDecisionData kingdomDecisionDerivedData = settlementClaimantDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is SettlementClaimantDecisionData);
            SettlementClaimantDecisionData deserializedObj = (SettlementClaimantDecisionData)obj;
            Assert.Equal(settlementClaimantDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(settlementClaimantDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(settlementClaimantDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(settlementClaimantDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(settlementClaimantDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(settlementClaimantDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(settlementClaimantDecisionData.SettlementId, deserializedObj.SettlementId);
            Assert.Equal(settlementClaimantDecisionData.CapturerHeroId, deserializedObj.CapturerHeroId);
            Assert.Equal(settlementClaimantDecisionData.ClanToExcludeId, deserializedObj.ClanToExcludeId);
        }

        [Fact]
        public void SettlementClaimantDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(SettlementClaimantDecisionData).GetField("SettlementField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            FieldInfo? fieldInfo2 = typeof(SettlementClaimantDecisionData).GetField("ClanToExcludeField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo2);
            FieldInfo? fieldInfo3 = typeof(SettlementClaimantDecisionData).GetField("CapturerHeroField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo3);
            object? obj = fieldInfo?.GetValue(null);
            Assert.NotNull(obj);
            object? obj2 = fieldInfo2?.GetValue(null);
            Assert.NotNull(obj2);
            object? obj3 = fieldInfo3?.GetValue(null);
            Assert.NotNull(obj3);
        }
    }
}
