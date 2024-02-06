using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class ExpelClanFromKingdomDecisionSerializationTest
    {
        [Fact]
        public void SerializeExpelClanFromKingdomDecision()
        {
            ExpelClanFromKingdomDecisionData expelClanFromKingdomDecisionData = new ExpelClanFromKingdomDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "Clan1", "Kingdom1");
            KingdomDecisionData kingdomDecisionDerivedData = expelClanFromKingdomDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is ExpelClanFromKingdomDecisionData);
            ExpelClanFromKingdomDecisionData deserializedObj = (ExpelClanFromKingdomDecisionData)obj;
            Assert.Equal(expelClanFromKingdomDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(expelClanFromKingdomDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(expelClanFromKingdomDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(expelClanFromKingdomDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(expelClanFromKingdomDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(expelClanFromKingdomDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(expelClanFromKingdomDecisionData.ClanToExpelId, deserializedObj.ClanToExpelId);
            Assert.Equal(expelClanFromKingdomDecisionData.OldKingdomId, deserializedObj.OldKingdomId);
        }
    }
}
