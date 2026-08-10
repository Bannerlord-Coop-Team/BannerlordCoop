using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Xunit;
using KingdomDecisionType = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms;

public class SettlementClaimantSnapshotRegistryTests
{
    [Fact]
    public void RegisteredSnapshot_CreatesOutcomesInAuthoritativeOrderWithMerits()
    {
        ObjectManager objectManager = CreateObjectManager();
        Clan firstClan = RegisterClan(objectManager, "first");
        Clan secondClan = RegisterClan(objectManager, "second");
        var decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        var registry = new SettlementClaimantSnapshotRegistry(objectManager);
        var candidates = new List<SettlementClaimantCandidateData>
        {
            new SettlementClaimantCandidateData(secondClan.StringId, 22.5f),
            new SettlementClaimantCandidateData(firstClan.StringId, 11.25f),
        };

        Assert.True(registry.TryRegister(decision, candidates));
        Assert.True(registry.TryCreateOutcomes(decision, out MBList<DecisionOutcome> outcomes));

        Assert.Collection(
            outcomes,
            outcome => AssertOutcome(outcome, secondClan, 22.5f),
            outcome => AssertOutcome(outcome, firstClan, 11.25f));
    }

    [Fact]
    public void RegisterSnapshot_MissingClan_ReturnsFalseWithoutSnapshot()
    {
        ObjectManager objectManager = CreateObjectManager();
        var decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        var registry = new SettlementClaimantSnapshotRegistry(objectManager);
        var candidates = new List<SettlementClaimantCandidateData>
        {
            new SettlementClaimantCandidateData("missing", 10f),
        };

        Assert.False(registry.TryRegister(decision, candidates));
        Assert.False(registry.TryCreateOutcomes(decision, out MBList<DecisionOutcome> outcomes));
        Assert.Null(outcomes);
    }

    [Fact]
    public void CapturedNarrowing_IsReusedWithoutRecalculatingMerits()
    {
        ObjectManager objectManager = CreateObjectManager();
        Clan firstClan = RegisterClan(objectManager, "first");
        Clan secondClan = RegisterClan(objectManager, "second");
        var decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        var registry = new SettlementClaimantSnapshotRegistry(objectManager);
        var narrowedOutcomes = new MBList<DecisionOutcome>
        {
            CreateOutcome(firstClan, 31f),
            CreateOutcome(secondClan, 17f),
        };

        registry.Capture(decision, narrowedOutcomes);
        Assert.True(registry.TryCreateOutcomes(decision, out MBList<DecisionOutcome> reconstructedOutcomes));

        Assert.Collection(
            reconstructedOutcomes,
            outcome => AssertOutcome(outcome, firstClan, 31f),
            outcome => AssertOutcome(outcome, secondClan, 17f));
        Assert.NotSame(narrowedOutcomes[0], reconstructedOutcomes[0]);
        Assert.NotSame(narrowedOutcomes[1], reconstructedOutcomes[1]);
    }

    [Fact]
    public void RegisterSnapshot_IdenticalRetrySucceedsAndConflictingRetryFails()
    {
        ObjectManager objectManager = CreateObjectManager();
        Clan clan = RegisterClan(objectManager, "claimant");
        var decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        var registry = new SettlementClaimantSnapshotRegistry(objectManager);

        Assert.True(registry.TryRegister(
            decision,
            new[] { new SettlementClaimantCandidateData(clan.StringId, 25f) }));
        Assert.True(registry.TryRegister(
            decision,
            new[] { new SettlementClaimantCandidateData(clan.StringId, 25f) }));
        Assert.False(registry.TryRegister(
            decision,
            new[] { new SettlementClaimantCandidateData(clan.StringId, 30f) }));

        Assert.True(registry.TryCreateOutcomes(decision, out MBList<DecisionOutcome> outcomes));
        Assert.Collection(outcomes, outcome => AssertOutcome(outcome, clan, 25f));
    }

    [Fact]
    public void JoinSnapshots_RoundTripByKingdomAndUnresolvedDecisionIndex()
    {
        ObjectManager serverObjectManager = CreateObjectManager();
        Clan serverFirstClan = RegisterClan(serverObjectManager, "first");
        Clan serverSecondClan = RegisterClan(serverObjectManager, "second");
        var serverDecision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        Kingdom serverKingdom = RegisterKingdom(
            serverObjectManager,
            "kingdom",
            ObjectHelper.SkipConstructor<KingSelectionKingdomDecision>(),
            serverDecision);
        var serverRegistry = new SettlementClaimantSnapshotRegistry(serverObjectManager);
        Assert.True(serverRegistry.TryRegister(
            serverDecision,
            new[]
            {
                new SettlementClaimantCandidateData(serverSecondClan.StringId, 22.5f),
                new SettlementClaimantCandidateData(serverFirstClan.StringId, 11.25f),
            }));

        Assert.True(serverRegistry.TryCreateJoinSnapshots(
            new[] { serverKingdom },
            out SettlementClaimantDecisionSnapshotData[] snapshots));
        SettlementClaimantDecisionSnapshotData snapshot = Assert.Single(snapshots);
        Assert.Equal("kingdom", snapshot.KingdomId);
        Assert.Equal(1, snapshot.DecisionIndex);

        ObjectManager clientObjectManager = CreateObjectManager();
        Clan clientFirstClan = RegisterClan(clientObjectManager, "first");
        Clan clientSecondClan = RegisterClan(clientObjectManager, "second");
        var clientDecision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        Kingdom clientKingdom = RegisterKingdom(
            clientObjectManager,
            "kingdom",
            ObjectHelper.SkipConstructor<KingSelectionKingdomDecision>(),
            clientDecision);
        var clientRegistry = new SettlementClaimantSnapshotRegistry(clientObjectManager);

        Assert.True(clientRegistry.TryApplyJoinSnapshots(new[] { clientKingdom }, snapshots));
        Assert.True(clientRegistry.TryCreateOutcomes(clientDecision, out MBList<DecisionOutcome> outcomes));
        Assert.Collection(
            outcomes,
            outcome => AssertOutcome(outcome, clientSecondClan, 22.5f),
            outcome => AssertOutcome(outcome, clientFirstClan, 11.25f));
    }

    [Fact]
    public void ApplyJoinSnapshots_MissingLoadedDecisionSnapshotFails()
    {
        ObjectManager objectManager = CreateObjectManager();
        var decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        Kingdom kingdom = RegisterKingdom(objectManager, "kingdom", decision);
        var registry = new SettlementClaimantSnapshotRegistry(objectManager);

        Assert.False(registry.TryApplyJoinSnapshots(
            new[] { kingdom },
            Array.Empty<SettlementClaimantDecisionSnapshotData>()));
        Assert.False(registry.TryCreateOutcomes(decision, out _));
    }

    [Fact]
    public void ApplyJoinSnapshots_ReplacesConflictingProvisionalLocalCapture()
    {
        ObjectManager objectManager = CreateObjectManager();
        Clan localClan = RegisterClan(objectManager, "local");
        Clan serverClan = RegisterClan(objectManager, "server");
        var decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        Kingdom kingdom = RegisterKingdom(objectManager, "kingdom", decision);
        var registry = new SettlementClaimantSnapshotRegistry(objectManager);
        registry.Capture(
            decision,
            new MBList<DecisionOutcome> { CreateOutcome(localClan, 30f) });
        var joinSnapshots = new[]
        {
            new SettlementClaimantDecisionSnapshotData(
                "kingdom",
                0,
                new[] { new SettlementClaimantCandidateData(serverClan.StringId, 20f) }),
        };

        Assert.True(registry.TryApplyJoinSnapshots(new[] { kingdom }, joinSnapshots));
        Assert.True(registry.TryCreateOutcomes(decision, out MBList<DecisionOutcome> outcomes));
        Assert.Collection(outcomes, outcome => AssertOutcome(outcome, serverClan, 20f));
    }

    [Fact]
    public void ApplyJoinSnapshots_RejectsConflictingSynchronizedSnapshot()
    {
        ObjectManager objectManager = CreateObjectManager();
        Clan firstServerClan = RegisterClan(objectManager, "first-server");
        Clan secondServerClan = RegisterClan(objectManager, "second-server");
        var decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        Kingdom kingdom = RegisterKingdom(objectManager, "kingdom", decision);
        var registry = new SettlementClaimantSnapshotRegistry(objectManager);
        Assert.True(registry.TryRegister(
            decision,
            new[] { new SettlementClaimantCandidateData(firstServerClan.StringId, 30f) }));
        var joinSnapshots = new[]
        {
            new SettlementClaimantDecisionSnapshotData(
                "kingdom",
                0,
                new[] { new SettlementClaimantCandidateData(secondServerClan.StringId, 20f) }),
        };

        Assert.False(registry.TryApplyJoinSnapshots(new[] { kingdom }, joinSnapshots));
        Assert.True(registry.TryCreateOutcomes(decision, out MBList<DecisionOutcome> outcomes));
        Assert.Collection(outcomes, outcome => AssertOutcome(outcome, firstServerClan, 30f));
    }

    private static ObjectManager CreateObjectManager()
    {
        return new ObjectManager(new LoggerConfiguration().CreateLogger());
    }

    private static Clan RegisterClan(ObjectManager objectManager, string clanId)
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        clan.StringId = clanId;
        objectManager.AddExisting(clanId, clan);
        return clan;
    }

    private static Kingdom RegisterKingdom(
        ObjectManager objectManager,
        string kingdomId,
        params KingdomDecisionType[] decisions)
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        kingdom.StringId = kingdomId;
        kingdom._unresolvedDecisions = new MBList<KingdomDecisionType>();
        foreach (KingdomDecisionType decision in decisions)
        {
            kingdom._unresolvedDecisions.Add(decision);
        }
        objectManager.AddExisting(kingdomId, kingdom);
        return kingdom;
    }

    private static SettlementClaimantDecision.ClanAsDecisionOutcome CreateOutcome(Clan clan, float merit)
    {
        return new SettlementClaimantDecision.ClanAsDecisionOutcome(clan)
        {
            InitialMerit = merit,
        };
    }

    private static void AssertOutcome(DecisionOutcome outcome, Clan expectedClan, float expectedMerit)
    {
        var claimantOutcome = Assert.IsType<SettlementClaimantDecision.ClanAsDecisionOutcome>(outcome);
        Assert.Same(expectedClan, claimantOutcome.Clan);
        Assert.Equal(expectedMerit, claimantOutcome.InitialMerit);
    }
}
