using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class SettlementClaimantDecisionSerializationTest
    {
        [Fact]
        public void SerializeSettlementClaimantDecision()
        {
            var candidates = new List<SettlementClaimantCandidateData>
            {
                new SettlementClaimantCandidateData("Clan2", 20.5f),
                new SettlementClaimantCandidateData("Clan3", 10.25f),
            };
            SettlementClaimantDecisionData settlementClaimantDecisionData = new SettlementClaimantDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "Settlement1", "Hero1", "Clan1", candidates);
            KingdomDecisionData kingdomDecisionDerivedData = settlementClaimantDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is SettlementClaimantDecisionData);
            SettlementClaimantDecisionData deserializedObj = (SettlementClaimantDecisionData)obj;
            Assert.Equal(settlementClaimantDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(settlementClaimantDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(settlementClaimantDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(settlementClaimantDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(settlementClaimantDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(settlementClaimantDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(settlementClaimantDecisionData.SettlementId, deserializedObj.SettlementId);
            Assert.Equal(settlementClaimantDecisionData.CapturerHeroId, deserializedObj.CapturerHeroId);
            Assert.Equal(settlementClaimantDecisionData.ClanToExcludeId, deserializedObj.ClanToExcludeId);
            Assert.Collection(
                deserializedObj.Candidates,
                candidate =>
                {
                    Assert.Equal("Clan2", candidate.ClanId);
                    Assert.Equal(20.5f, candidate.InitialMerit);
                },
                candidate =>
                {
                    Assert.Equal("Clan3", candidate.ClanId);
                    Assert.Equal(10.25f, candidate.InitialMerit);
                });
        }

        [Fact]
        public void SettlementClaimantDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(SettlementClaimantDecisionData).GetField("SettlementField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            FieldInfo? fieldInfo2 = typeof(SettlementClaimantDecisionData).GetField("ClanToExcludeField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo2);
            FieldInfo? fieldInfo3 = typeof(SettlementClaimantDecisionData).GetField("CapturerHeroField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo3);
            object? obj = fieldInfo?.GetValue(null);
            Assert.NotNull(obj);
            object? obj2 = fieldInfo2?.GetValue(null);
            Assert.NotNull(obj2);
            object? obj3 = fieldInfo3?.GetValue(null);
            Assert.NotNull(obj3);
        }

        [Fact]
        public void RoundTrip_WithNullCapturerAndClanToExclude_DoesNotThrowAndPreservesNulls()
        {
            // Vanilla raises this decision with capturerHero and clanToExclude both null
            // (e.g. a fief reassignment after a clan leaves or dies), so both ids are null on the wire.
            ObjectManager objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
            Clan proposerClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            proposerClan.StringId = "ProposerClan";
            Kingdom kingdom = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
            kingdom.StringId = "Kingdom";
            Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            settlement.StringId = "Settlement";
            Clan candidateClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            candidateClan.StringId = "CandidateClan";
            objectManager.AddExisting(proposerClan.StringId, proposerClan);
            objectManager.AddExisting(kingdom.StringId, kingdom);
            objectManager.AddExisting(settlement.StringId, settlement);
            objectManager.AddExisting(candidateClan.StringId, candidateClan);
            var candidates = new List<SettlementClaimantCandidateData>
            {
                new SettlementClaimantCandidateData(candidateClan.StringId, 42f),
            };
            SettlementClaimantDecisionData data = new SettlementClaimantDecisionData(
                proposerClan.StringId, kingdom.StringId, 10, true, true, true, settlement.StringId, null, null, candidates);

            // Deserialization: null optional ids must reconstruct the decision, not drop it.
            Assert.True(data.TryGetKingdomDecision(objectManager, out var kingdomDecision));
            Assert.True(kingdomDecision is SettlementClaimantDecision);
            SettlementClaimantDecision decision = (SettlementClaimantDecision)kingdomDecision;
            Assert.Null(decision._capturerHero);
            Assert.Null(decision.ClanToExclude);
            Assert.Same(settlement, decision.Settlement);

            // Serialization: converting back must not throw on the null fields and must keep them null.
            var snapshotRegistry = new SettlementClaimantSnapshotRegistry(objectManager);
            Assert.True(snapshotRegistry.TryRegister(decision, candidates));
            KingdomDecisionData roundTrippedData = new KingdomDecisionDataConverter(objectManager, snapshotRegistry).Convert(decision);
            Assert.True(roundTrippedData is SettlementClaimantDecisionData);
            SettlementClaimantDecisionData roundTripped = (SettlementClaimantDecisionData)roundTrippedData;
            Assert.Null(roundTripped.CapturerHeroId);
            Assert.Null(roundTripped.ClanToExcludeId);
            Assert.Equal(settlement.StringId, roundTripped.SettlementId);
            SettlementClaimantCandidateData candidate = Assert.Single(roundTripped.Candidates);
            Assert.Equal(candidateClan.StringId, candidate.ClanId);
            Assert.Equal(42f, candidate.InitialMerit);
        }
    }
}
