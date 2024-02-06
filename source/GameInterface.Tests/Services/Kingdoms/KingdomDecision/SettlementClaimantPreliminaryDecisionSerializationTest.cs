using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class SettlementClaimantPreliminaryDecisionSerializationTest
    {
        [Fact]
        public void SerializeExpelClanFromKingdomDecision()
        {
            SettlementClaimantPreliminaryDecisionData settlementClaimantPreliminaryDecisionData = new SettlementClaimantPreliminaryDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "Settlement1", "Clan1");
            KingdomDecisionData kingdomDecisionDerivedData = settlementClaimantPreliminaryDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is SettlementClaimantPreliminaryDecisionData);
            SettlementClaimantPreliminaryDecisionData deserializedObj = (SettlementClaimantPreliminaryDecisionData)obj;
            Assert.Equal(settlementClaimantPreliminaryDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(settlementClaimantPreliminaryDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(settlementClaimantPreliminaryDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(settlementClaimantPreliminaryDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(settlementClaimantPreliminaryDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(settlementClaimantPreliminaryDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(settlementClaimantPreliminaryDecisionData.SettlementId, deserializedObj.SettlementId);
            Assert.Equal(settlementClaimantPreliminaryDecisionData.OwnerClanId, deserializedObj.OwnerClanId);
        }
    }
}
