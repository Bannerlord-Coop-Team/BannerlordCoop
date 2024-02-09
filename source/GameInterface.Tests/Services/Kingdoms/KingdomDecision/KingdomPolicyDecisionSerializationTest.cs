using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class KingdomPolicyDecisionSerializationTest
    {
        [Fact]
        public void SerializeKingdomPolicyDecision()
        {
            KingdomPolicyDecisionData kingdomPolicyDecisionData = new KingdomPolicyDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "PolicyObject1", true, new List<string>() { "PolicyObject2", "PolicyObject3" });
            KingdomDecisionData kingdomDecisionDerivedData = kingdomPolicyDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is KingdomPolicyDecisionData);
            KingdomPolicyDecisionData deserializedObj = (KingdomPolicyDecisionData)obj;
            Assert.Equal(kingdomPolicyDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(kingdomPolicyDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(kingdomPolicyDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(kingdomPolicyDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(kingdomPolicyDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(kingdomPolicyDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(kingdomPolicyDecisionData.PolicyObjectId, deserializedObj.PolicyObjectId);
            Assert.Equal(kingdomPolicyDecisionData.KingdomPolicies, deserializedObj.KingdomPolicies);
            Assert.Equal(kingdomPolicyDecisionData.IsInvertedDecision, deserializedObj.IsInvertedDecision);
        }

        [Fact]
        public void KingdomPolicyDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(KingdomPolicyDecisionData).GetField("PolicyField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            FieldInfo? fieldInfo2 = typeof(KingdomPolicyDecisionData).GetField("IsInvertedDecisionField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo2);
            FieldInfo? fieldInfo3 = typeof(KingdomPolicyDecisionData).GetField("KingdomPoliciesField", BindingFlags.Static | BindingFlags.NonPublic);
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
