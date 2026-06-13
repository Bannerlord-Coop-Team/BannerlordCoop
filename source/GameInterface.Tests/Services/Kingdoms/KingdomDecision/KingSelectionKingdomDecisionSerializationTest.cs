using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using Serilog;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

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

        [Fact]
        public void RoundTrip_WithNullClanToExclude_DoesNotThrowAndPreservesNull()
        {
            // KingSelectionKingdomDecision's clanToExclude ctor arg defaults to null, so the id is null on the wire.
            ObjectManager objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
            Clan proposerClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            proposerClan.StringId = "ProposerClan";
            Kingdom kingdom = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
            kingdom.StringId = "Kingdom";
            objectManager.AddExisting(proposerClan.StringId, proposerClan);
            objectManager.AddExisting(kingdom.StringId, kingdom);

            KingSelectionKingdomDecisionData data = new KingSelectionKingdomDecisionData(
                proposerClan.StringId, kingdom.StringId, 10, true, true, true, null);

            // Deserialization: a null clanToExclude id must reconstruct the decision, not drop it.
            Assert.True(data.TryGetKingdomDecision(objectManager, out var kingdomDecision));
            Assert.True(kingdomDecision is KingSelectionKingdomDecision);
            KingSelectionKingdomDecision decision = (KingSelectionKingdomDecision)kingdomDecision;
            Assert.Null(decision._clanToExclude);

            // Serialization: converting back must not throw on the null field and must keep it null.
            KingdomDecisionData roundTrippedData = decision.ToKingdomDecisionData();
            Assert.True(roundTrippedData is KingSelectionKingdomDecisionData);
            KingSelectionKingdomDecisionData roundTripped = (KingSelectionKingdomDecisionData)roundTrippedData;
            Assert.Null(roundTripped.ClanToExcludeId);
        }
    }
}
