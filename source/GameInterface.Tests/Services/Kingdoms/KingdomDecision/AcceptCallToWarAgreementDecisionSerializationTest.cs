using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class AcceptCallToWarAgreementDecisionSerializationTest
    {
        [Fact]
        public void SerializeAcceptCallToWarAgreementDecision()
        {
            AcceptCallToWarAgreementDecisionData acceptCallToWarAgreementDecisionData = new AcceptCallToWarAgreementDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "CallingKingdom", "WarTargetKingdom", 500);
            KingdomDecisionData kingdomDecisionDerivedData = acceptCallToWarAgreementDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is AcceptCallToWarAgreementDecisionData);
            AcceptCallToWarAgreementDecisionData deserializedObj = (AcceptCallToWarAgreementDecisionData)obj;
            Assert.Equal(acceptCallToWarAgreementDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(acceptCallToWarAgreementDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(acceptCallToWarAgreementDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(acceptCallToWarAgreementDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(acceptCallToWarAgreementDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(acceptCallToWarAgreementDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(acceptCallToWarAgreementDecisionData.CallingKingdomId, deserializedObj.CallingKingdomId);
            Assert.Equal(acceptCallToWarAgreementDecisionData.KingdomToCallToWarAgainstId, deserializedObj.KingdomToCallToWarAgainstId);
            Assert.Equal(acceptCallToWarAgreementDecisionData.CallToWarCost, deserializedObj.CallToWarCost);
        }

        [Fact]
        public void AcceptCallToWarAgreementDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(AcceptCallToWarAgreementDecisionData).GetField("CallToWarCostField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            object? obj = fieldInfo?.GetValue(null);
            Assert.NotNull(obj);
        }
    }
}
