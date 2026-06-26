using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class ProposeCallToWarAgreementDecisionSerializationTest
    {
        [Fact]
        public void SerializeProposeCallToWarAgreementDecision()
        {
            ProposeCallToWarAgreementDecisionData proposeCallToWarAgreementDecisionData = new ProposeCallToWarAgreementDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "CalledKingdom", "WarTargetKingdom", 500);
            KingdomDecisionData kingdomDecisionDerivedData = proposeCallToWarAgreementDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is ProposeCallToWarAgreementDecisionData);
            ProposeCallToWarAgreementDecisionData deserializedObj = (ProposeCallToWarAgreementDecisionData)obj;
            Assert.Equal(proposeCallToWarAgreementDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(proposeCallToWarAgreementDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(proposeCallToWarAgreementDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(proposeCallToWarAgreementDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(proposeCallToWarAgreementDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(proposeCallToWarAgreementDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(proposeCallToWarAgreementDecisionData.CalledKingdomId, deserializedObj.CalledKingdomId);
            Assert.Equal(proposeCallToWarAgreementDecisionData.KingdomToCallToWarAgainstId, deserializedObj.KingdomToCallToWarAgainstId);
            Assert.Equal(proposeCallToWarAgreementDecisionData.CallToWarCost, deserializedObj.CallToWarCost);
        }

        [Fact]
        public void ProposeCallToWarAgreementDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(ProposeCallToWarAgreementDecisionData).GetField("CalledKingdomField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            FieldInfo? fieldInfo2 = typeof(ProposeCallToWarAgreementDecisionData).GetField("KingdomToCallToWarAgainstField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo2);
            FieldInfo? fieldInfo3 = typeof(ProposeCallToWarAgreementDecisionData).GetField("CallToWarCostField", BindingFlags.Static | BindingFlags.NonPublic);
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
