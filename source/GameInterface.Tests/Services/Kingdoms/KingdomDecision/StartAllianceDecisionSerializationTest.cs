using GameInterface.Services.Kingdoms.Commands;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.ObjectManager;
using Moq;
using ProtoBuf;
using Serilog;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class StartAllianceDecisionSerializationTest
    {
        [Fact]
        public void SerializeStartAllianceDecision()
        {
            StartAllianceDecisionData startAllianceDecisionData = new StartAllianceDecisionData(
                "ProposerClan", "Kingdom", 10, true, true, true, "TargetKingdom", true,
                "decision_kingdom", "target_kingdom");
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
            Assert.Equal(startAllianceDecisionData.DecisionKingdomStringId, deserializedObj.DecisionKingdomStringId);
            Assert.Equal(startAllianceDecisionData.KingdomToStartAllianceWithStringId, deserializedObj.KingdomToStartAllianceWithStringId);
            Assert.True(deserializedObj.IsProposedByOpponent);
        }

        [Fact]
        public void StartAllianceDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(StartAllianceDecisionData).GetField("KingdomToStartAllianceWithField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            object? obj = fieldInfo?.GetValue(null);
            Assert.NotNull(obj);
        }

        [Fact]
        public void CompactClanReference_MatchesRegisteredPlayerClan()
        {
            var objectManager = new ObjectManager(Mock.Of<ILogger>());
            var playerClan = new Clan();
            const string registeredClanId = "Clan_Created_27";
            Assert.True(objectManager.AddExisting(registeredClanId, playerClan));

            Assert.True(KingdomDebugCommand.MatchesRegisteredReference(
                objectManager,
                "Created_27",
                playerClan,
                typeof(Clan)));
            Assert.True(KingdomDebugCommand.MatchesRegisteredReference(
                objectManager,
                registeredClanId,
                playerClan,
                typeof(Clan)));
            Assert.False(KingdomDebugCommand.MatchesRegisteredReference(
                objectManager,
                "Created_28",
                playerClan,
                typeof(Clan)));
        }
    }
}
