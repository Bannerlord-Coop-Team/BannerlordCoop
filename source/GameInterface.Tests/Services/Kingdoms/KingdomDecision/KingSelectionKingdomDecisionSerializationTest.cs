using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class KingSelectionKingdomDecisionSerializationTest
    {
        [Fact]
        public void SerializeKingSelectionKingdomDecision()
        {
            KingSelectionKingdomDecisionData kingSelectionKingdomDecisionData = new KingSelectionKingdomDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "ClanToExclude");
            KingdomDecisionData kingdomDecisionDerivedData = kingSelectionKingdomDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is KingSelectionKingdomDecisionData);
            KingSelectionKingdomDecisionData deserializedObj = (KingSelectionKingdomDecisionData)obj;
            Assert.Equal(kingSelectionKingdomDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(kingSelectionKingdomDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(kingSelectionKingdomDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(kingSelectionKingdomDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(kingSelectionKingdomDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(kingSelectionKingdomDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(kingSelectionKingdomDecisionData.ClanToExcludeId, deserializedObj.ClanToExcludeId);

        }
    }
}
