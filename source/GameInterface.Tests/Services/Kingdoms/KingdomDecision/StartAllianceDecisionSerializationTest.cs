using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class StartAllianceDecisionSerializationTest
    {
        [Fact]
        public void SerializeStartAllianceDecision()
        {
            StartAllianceDecisionData startAllianceDecisionData = new StartAllianceDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "TargetKingdom");
            KingdomDecisionData kingdomDecisionDerivedData = startAllianceDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is StartAllianceDecisionData);
            StartAllianceDecisionData deserializedObj = (StartAllianceDecisionData)obj;
            Assert.Equal(startAllianceDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(startAllianceDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(startAllianceDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(startAllianceDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(startAllianceDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(startAllianceDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(startAllianceDecisionData.KingdomToStartAllianceWithId, deserializedObj.KingdomToStartAllianceWithId);
        }

        [Fact]
        public void StartAllianceDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(StartAllianceDecisionData).GetField("KingdomToStartAllianceWithField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            FieldInfo? fieldInfo2 = typeof(StartAllianceDecisionData).GetField("AllianceCampaignBehaviorField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo2);
            object? obj = fieldInfo?.GetValue(null);
            Assert.NotNull(obj);
            object? obj2 = fieldInfo2?.GetValue(null);
            Assert.NotNull(obj2);
        }
    }
}
