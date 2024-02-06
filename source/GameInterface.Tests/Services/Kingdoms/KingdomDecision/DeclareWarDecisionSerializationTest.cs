using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    /// <summary>
    /// Class for testing the serialization of DeclareWarDecisionData.
    /// </summary>
    public class DeclareWarDecisionSerializationTest
    {
        [Fact]
        public void SerializeDeclareWarDecisionDataWithClanFaction()
        {
            DeclareWarDecisionData DeclareWarDecision = new DeclareWarDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "Clan1");
            KingdomDecisionData kingdomDecisionData = DeclareWarDecision;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is DeclareWarDecisionData);
            DeclareWarDecisionData deserializedObj = (DeclareWarDecisionData)obj;
            Assert.Equal(DeclareWarDecision.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(DeclareWarDecision.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(DeclareWarDecision.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(DeclareWarDecision.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(DeclareWarDecision.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(DeclareWarDecision.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(DeclareWarDecision.FactionToDeclareWarOnId, deserializedObj.FactionToDeclareWarOnId);
        }
    }
}
