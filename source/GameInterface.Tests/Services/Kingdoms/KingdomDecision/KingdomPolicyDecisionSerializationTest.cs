using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

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
        public void EmptyPolicySnapshot_RoundTripReconstructsDecision()
        {
            var data = new KingdomPolicyDecisionData(
                "ProposerClan", "Kingdom", 10, true, true, true, "PolicyObject1", true, new List<string>());
            using var stream = new MemoryStream();
            Serializer.Serialize<KingdomDecisionData>(stream, data);
            stream.Position = 0;
            var deserialized = Assert.IsType<KingdomPolicyDecisionData>(
                Serializer.Deserialize<KingdomDecisionData>(stream));

            var objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
            var proposerClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            proposerClan.StringId = data.ProposerClanId;
            var kingdom = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
            kingdom.StringId = data.KingdomId;
            var policy = (PolicyObject)FormatterServices.GetUninitializedObject(typeof(PolicyObject));
            policy.StringId = data.PolicyObjectId;
            objectManager.AddExisting(proposerClan.StringId, proposerClan);
            objectManager.AddExisting(kingdom.StringId, kingdom);
            objectManager.AddExisting(policy.StringId, policy);

            Assert.True(deserialized.TryGetKingdomDecision(objectManager, out var decision));
            var policyDecision = Assert.IsType<KingdomPolicyDecision>(decision);
            Assert.Empty(policyDecision._kingdomPolicies);
        }

        [Fact]
        public void KingdomPolicyDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(KingdomPolicyDecisionData).GetField("PolicyField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            object? obj = fieldInfo?.GetValue(null);
            Assert.NotNull(obj);
        }
    }
}
