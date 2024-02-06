﻿using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;
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
    }
}
