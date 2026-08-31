using Common.Util;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Services.SiegeEvents;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

[Collection(nameof(CampaignCurrentCollection))]
public class MobilePartyBehaviorSnapshotTests
{
    [Fact]
    public void TryCreate_RegisteredMoveTarget_PreservesPartyNavigation()
    {
        var party = CreateParty();
        var moveTarget = ObjectHelper.SkipConstructor<MobileParty>();
        party.MoveTargetParty = moveTarget;

        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_Created_1";
        string moveTargetId = "MobileParty_Created_2";
        objectManager.Setup(m => m.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(moveTarget, out moveTargetId)).Returns(true);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        Assert.True(snapshot.TryCreate(party, out PartyBehaviorUpdateData data));
        Assert.Equal(MoveModeType.Party, data.PartyMoveMode);
        Assert.Equal("Created_2", data.MoveTargetPartyId);
    }

    [Fact]
    public void TryCreate_RemovedMoveTarget_UsesLastTargetPoint()
    {
        var party = CreateParty();
        var removedMoveTarget = ObjectHelper.SkipConstructor<MobileParty>();
        removedMoveTarget._position = new CampaignVec2(new Vec2(30f, 40f), isOnLand: true);
        party.MoveTargetParty = removedMoveTarget;

        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_Created_1";
        string missingId = null!;
        objectManager.Setup(m => m.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(removedMoveTarget, out missingId)).Returns(false);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        Assert.True(snapshot.TryCreate(party, out PartyBehaviorUpdateData data));
        Assert.Equal(MoveModeType.Point, data.PartyMoveMode);
        Assert.Null(data.MoveTargetPartyId);
        Assert.Equal(removedMoveTarget.Position, data.MoveTargetPoint);
    }

    [Fact]
    public void TryCreate_UnregisteredInteractable_DoesNotMutateParty()
    {
        var party = CreateParty();
        var removedTarget = CreatePartyWithPartyBase();
        party._defaultBehavior = AiBehavior.EngageParty;
        party.ShortTermBehavior = AiBehavior.EngageParty;
        party.Ai.AiBehaviorInteractable = removedTarget.Party;

        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_Created_1";
        string missingId = null!;
        objectManager.Setup(m => m.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(removedTarget.Party, out missingId)).Returns(false);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        Assert.False(snapshot.TryCreate(party, out _));
        Assert.Same(removedTarget.Party, party.Ai.AiBehaviorInteractable);
        Assert.Equal(AiBehavior.EngageParty, party.DefaultBehavior);
        Assert.Equal(AiBehavior.EngageParty, party.ShortTermBehavior);
    }

    [Fact]
    public void TryCreateJoinState_ValidReferences_PreservesBehavior()
    {
        var party = CreateParty();
        var target = CreatePartyWithPartyBase();
        party._defaultBehavior = AiBehavior.EngageParty;
        party.ShortTermBehavior = AiBehavior.EngageParty;
        party.Ai.AiBehaviorInteractable = target.Party;
        party.TargetParty = target;
        party.MoveTargetParty = target;

        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_Created_1";
        string targetId = "MobileParty_Created_2";
        string targetPartyBaseId = "PartyBase_Created_2";
        objectManager.Setup(m => m.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(target, out targetId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(target.Party, out targetPartyBaseId)).Returns(true);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        bool created = snapshot.TryCreateJoinState(
            party,
            LiveParties(party, target),
            LiveSettlements(),
            out MobilePartyJoinState state,
            out string failure);

        Assert.True(created, failure);
        Assert.Null(failure);
        Assert.Equal(AiBehavior.EngageParty, party.DefaultBehavior);
        Assert.Same(target.Party, party.Ai.AiBehaviorInteractable);
        Assert.Same(target, party.TargetParty);
        Assert.Same(target, party.MoveTargetParty);
        Assert.Equal(MoveModeType.Party, state.Behavior.PartyMoveMode);
        Assert.Equal("Created_2", state.Behavior.TargetPartyId);
        Assert.Equal("Created_2", state.Behavior.MoveTargetPartyId);
        Assert.Equal("Created_2", state.Behavior.InteractablePointId);
    }

    [Fact]
    public void TryCreateJoinState_RemovedMoveTarget_UsesLastTargetPoint()
    {
        var party = CreateParty();
        var removedMoveTarget = ObjectHelper.SkipConstructor<MobileParty>();
        removedMoveTarget._position = new CampaignVec2(new Vec2(30f, 40f), isOnLand: true);
        party.MoveTargetParty = removedMoveTarget;

        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_Created_1";
        string missingId = null!;
        objectManager.Setup(m => m.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(removedMoveTarget, out missingId)).Returns(false);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        bool created = snapshot.TryCreateJoinState(
            party,
            LiveParties(party),
            LiveSettlements(),
            out MobilePartyJoinState state,
            out string failure);

        Assert.True(created, failure);
        Assert.Null(failure);
        Assert.Same(removedMoveTarget, party.MoveTargetParty);
        Assert.Equal(MoveModeType.Party, party.PartyMoveMode);
        Assert.Equal(MoveModeType.Point, state.Behavior.PartyMoveMode);
        Assert.Null(state.Behavior.MoveTargetPartyId);
        Assert.Equal(removedMoveTarget.Position, state.Behavior.MoveTargetPoint);
    }

    [Fact]
    public void TryCreateJoinState_RegisteredNonLiveMoveTarget_UsesLastTargetPoint()
    {
        var party = CreateParty();
        var removedMoveTarget = ObjectHelper.SkipConstructor<MobileParty>();
        removedMoveTarget._position = new CampaignVec2(new Vec2(30f, 40f), isOnLand: true);
        party.MoveTargetParty = removedMoveTarget;

        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_Created_1";
        string removedMoveTargetId = "MobileParty_Created_2";
        objectManager.Setup(m => m.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(removedMoveTarget, out removedMoveTargetId)).Returns(true);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        bool created = snapshot.TryCreateJoinState(
            party,
            LiveParties(party),
            LiveSettlements(),
            out MobilePartyJoinState state,
            out string failure);

        Assert.True(created, failure);
        Assert.Null(failure);
        Assert.Same(removedMoveTarget, party.MoveTargetParty);
        Assert.Equal(MoveModeType.Party, party.PartyMoveMode);
        Assert.Equal(MoveModeType.Point, state.Behavior.PartyMoveMode);
        Assert.Null(state.Behavior.MoveTargetPartyId);
        Assert.Equal(removedMoveTarget.Position, state.Behavior.MoveTargetPoint);
    }

    [Fact]
    public void TryCreateJoinState_UnregisteredReferences_UsesPointSnapshotWithoutMutatingParty()
    {
        var party = CreateParty();
        var removedTarget = CreatePartyWithPartyBase();
        SetPartyTargets(party, removedTarget);

        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_Created_1";
        string missingPartyId = null!;
        string missingPartyBaseId = null!;
        objectManager.Setup(m => m.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(removedTarget, out missingPartyId)).Returns(false);
        objectManager.Setup(m => m.TryGetId(removedTarget.Party, out missingPartyBaseId)).Returns(false);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        bool created = TryCreateJoinStateWithCampaign(
            snapshot,
            party,
            LiveParties(party),
            LiveSettlements(),
            out MobilePartyJoinState state,
            out string failure);

        Assert.True(created, failure);
        Assert.Null(failure);
        AssertPartyTargetsUnchanged(party, removedTarget);
        AssertPointSnapshotWithoutTargets(
            state,
            removedTarget.Position,
            MobileParty.NavigationType.Default);
    }

    [Fact]
    public void TryCreateJoinState_RegisteredNonLiveReferences_UsesPointSnapshotWithoutMutatingParty()
    {
        var party = CreateParty();
        var removedTarget = CreatePartyWithPartyBase();
        SetPartyTargets(party, removedTarget);

        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_Created_1";
        string removedTargetId = "MobileParty_Created_2";
        string removedPartyBaseId = "PartyBase_Created_2";
        objectManager.Setup(m => m.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(removedTarget, out removedTargetId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(removedTarget.Party, out removedPartyBaseId)).Returns(true);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        bool created = TryCreateJoinStateWithCampaign(
            snapshot,
            party,
            LiveParties(party),
            LiveSettlements(),
            out MobilePartyJoinState state,
            out string failure);

        Assert.True(created, failure);
        Assert.Null(failure);
        AssertPartyTargetsUnchanged(party, removedTarget);
        AssertPointSnapshotWithoutTargets(
            state,
            removedTarget.Position,
            MobileParty.NavigationType.Default);
    }

    [Fact]
    public void TryCreateJoinState_RegisteredNonLiveSettlement_UsesPointSnapshotWithoutMutatingParty()
    {
        var party = CreateParty();
        var removedSettlement = ObjectHelper.SkipConstructor<Settlement>();
        party._defaultBehavior = AiBehavior.GoToSettlement;
        party.ShortTermBehavior = AiBehavior.GoToSettlement;
        party._targetSettlement = removedSettlement;

        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_Created_1";
        string removedSettlementId = "Settlement_town_ES1";
        objectManager.Setup(m => m.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(removedSettlement, out removedSettlementId)).Returns(true);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        bool created = TryCreateJoinStateWithCampaign(
            snapshot,
            party,
            LiveParties(party),
            LiveSettlements(),
            out MobilePartyJoinState state,
            out string failure);

        Assert.True(created, failure);
        Assert.Null(failure);
        Assert.Equal(AiBehavior.GoToSettlement, party.DefaultBehavior);
        Assert.Equal(AiBehavior.GoToSettlement, party.ShortTermBehavior);
        Assert.Same(removedSettlement, party.TargetSettlement);
        Assert.Equal(MoveModeType.Party, party.PartyMoveMode);
        AssertPointSnapshotWithoutTargets(
            state,
            party.MoveTargetPoint,
            party.DesiredAiNavigationType);
    }

    [Fact]
    public void TryCreateJoinState_StaleReferencesOnHeldParty_HoldsSnapshotWithoutMutatingParty()
    {
        var party = CreateParty();
        var removedTarget = CreatePartyWithPartyBase();
        SetPartyTargets(party, removedTarget);
        party._defaultBehavior = AiBehavior.Hold;
        party.ShortTermBehavior = AiBehavior.Hold;
        party.PartyMoveMode = MoveModeType.Hold;

        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_Created_1";
        string missingPartyId = null!;
        string missingPartyBaseId = null!;
        objectManager.Setup(m => m.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(removedTarget, out missingPartyId)).Returns(false);
        objectManager.Setup(m => m.TryGetId(removedTarget.Party, out missingPartyBaseId)).Returns(false);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        bool created = snapshot.TryCreateJoinState(
            party,
            LiveParties(party),
            LiveSettlements(),
            out MobilePartyJoinState state,
            out string failure);

        Assert.True(created, failure);
        Assert.Null(failure);
        Assert.Equal(AiBehavior.Hold, party.DefaultBehavior);
        Assert.Equal(AiBehavior.Hold, party.ShortTermBehavior);
        Assert.Equal(MoveModeType.Hold, party.PartyMoveMode);
        Assert.Same(removedTarget.Party, party.Ai.AiBehaviorInteractable);
        Assert.Same(removedTarget, party.TargetParty);
        Assert.Same(removedTarget, party.MoveTargetParty);
        AssertHeldSnapshotWithoutTargets(state, party.MoveTargetPoint);
    }

    [Fact]
    public void TryCreateJoinState_MissingAi_ReportsFailure()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var snapshot = new MobilePartyBehaviorSnapshot(Mock.Of<IObjectManager>());

        bool created = snapshot.TryCreateJoinState(
            party,
            LiveParties(party),
            LiveSettlements(),
            out _,
            out string failure);

        Assert.False(created);
        Assert.Equal("party AI is unavailable", failure);
    }

    [Fact]
    public void TryApplyJoinBaseline_MismatchedPartyCount_ReportsCounts()
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            campaign.CampaignObjectManager = new CampaignObjectManager
            {
                Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
            };
            Campaign.Current = campaign;
            var snapshot = new MobilePartyBehaviorSnapshot(Mock.Of<IObjectManager>());

            bool applied = snapshot.TryApplyJoinBaseline(
                new MobilePartyJoinState[1],
                () => { });

            Assert.False(applied);
            Assert.Equal(
                "party count mismatch (baseline=1, client=0)",
                snapshot.LastJoinBaselineFailure);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }

    [Fact]
    public void TryApplyJoinBaseline_InactiveParty_DoesNotCountTowardCoverage()
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            var inactiveParty = CreateParty();
            inactiveParty.IsActive = false;
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            var campaignObjectManager = new CampaignObjectManager
            {
                Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
            };
            campaignObjectManager._mobileParties.Add(inactiveParty);
            campaign.CampaignObjectManager = campaignObjectManager;
            Campaign.Current = campaign;
            var snapshot = new MobilePartyBehaviorSnapshot(Mock.Of<IObjectManager>());

            bool applied = snapshot.TryApplyJoinBaseline(
                Array.Empty<MobilePartyJoinState>(),
                () => { });

            Assert.True(applied);
            Assert.Null(snapshot.LastJoinBaselineFailure);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }

    [Fact]
    public void TryApplyJoinBaseline_MissingActiveParty_ReportsActiveCount()
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            var campaignObjectManager = new CampaignObjectManager
            {
                Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
            };
            campaignObjectManager._mobileParties.Add(CreateParty());
            campaignObjectManager._mobileParties.Add(CreateParty());
            var inactiveParty = CreateParty();
            inactiveParty.IsActive = false;
            campaignObjectManager._mobileParties.Add(inactiveParty);
            campaign.CampaignObjectManager = campaignObjectManager;
            Campaign.Current = campaign;
            var snapshot = new MobilePartyBehaviorSnapshot(Mock.Of<IObjectManager>());

            bool applied = snapshot.TryApplyJoinBaseline(
                new MobilePartyJoinState[1],
                () => { });

            Assert.False(applied);
            Assert.Equal(
                "party count mismatch (baseline=1, client=2)",
                snapshot.LastJoinBaselineFailure);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }

    [Fact]
    public void TryApplyJoinBaseline_MissingPartyId_ReportsStateIndex()
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            var campaignObjectManager = new CampaignObjectManager
            {
                Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
            };
            campaignObjectManager._mobileParties.Add(CreateParty());
            campaign.CampaignObjectManager = campaignObjectManager;
            Campaign.Current = campaign;
            var snapshot = new MobilePartyBehaviorSnapshot(Mock.Of<IObjectManager>());

            bool applied = snapshot.TryApplyJoinBaseline(
                new[] { new MobilePartyJoinState() },
                () => { });

            Assert.False(applied);
            Assert.Equal("state 0 has no mobile-party id", snapshot.LastJoinBaselineFailure);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }

    [Fact]
    public void TryApplyJoinBaseline_UnresolvedInteractable_ReportsPartyAndDependency()
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            var party = CreateParty();
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            var campaignObjectManager = new CampaignObjectManager
            {
                Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
            };
            campaignObjectManager._mobileParties.Add(party);
            campaign.CampaignObjectManager = campaignObjectManager;
            Campaign.Current = campaign;

            var objectManager = new Mock<IObjectManager>();
            MobileParty resolvedParty = party;
            objectManager
                .Setup(m => m.TryGetObject("Created_1", out resolvedParty))
                .Returns(true);
            var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);
            var behavior = new PartyBehaviorUpdateData(
                "Created_1",
                AiBehavior.Hold,
                "MissingPartyBase",
                default,
                default,
                AiBehavior.Hold,
                default,
                default);

            bool applied = snapshot.TryApplyJoinBaseline(
                new[] { new MobilePartyJoinState { Behavior = behavior } },
                () => { });

            Assert.False(applied);
            Assert.Equal(
                "state 0 party 'Created_1' failed validation: " +
                "interactable 'MissingPartyBase' could not be resolved",
                snapshot.LastJoinBaselineFailure);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }

    [Fact]
    public void TryApplyJoinBaseline_SuccessClearsPreviousFailure()
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            campaign.CampaignObjectManager = new CampaignObjectManager
            {
                Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
            };
            Campaign.Current = campaign;
            var snapshot = new MobilePartyBehaviorSnapshot(Mock.Of<IObjectManager>());

            Assert.False(snapshot.TryApplyJoinBaseline(new MobilePartyJoinState[1], () => { }));
            Assert.NotNull(snapshot.LastJoinBaselineFailure);
            Assert.Equal(1, snapshot.LoggedJoinBaselineFailureCount);

            Assert.True(snapshot.TryApplyJoinBaseline(Array.Empty<MobilePartyJoinState>(), () => { }));
            Assert.Null(snapshot.LastJoinBaselineFailure);
            Assert.Equal(0, snapshot.LoggedJoinBaselineFailureCount);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }

    [Fact]
    public void TryApplyJoinBaseline_AlternatingRetriesTrackEachFailureOnce()
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            var campaignObjectManager = new CampaignObjectManager
            {
                Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
            };
            campaignObjectManager._mobileParties.Add(CreateParty());
            campaign.CampaignObjectManager = campaignObjectManager;
            Campaign.Current = campaign;
            var snapshot = new MobilePartyBehaviorSnapshot(Mock.Of<IObjectManager>());
            var missingIdBaseline = new[] { new MobilePartyJoinState() };

            Assert.False(snapshot.TryApplyJoinBaseline(missingIdBaseline, () => { }));
            Assert.False(snapshot.TryApplyJoinBaseline(Array.Empty<MobilePartyJoinState>(), () => { }));
            Assert.False(snapshot.TryApplyJoinBaseline(missingIdBaseline, () => { }));

            Assert.Equal("state 0 has no mobile-party id", snapshot.LastJoinBaselineFailure);
            Assert.Equal(2, snapshot.LoggedJoinBaselineFailureCount);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }

    [Fact]
    public void TryCreateJoinState_UnregisteredParty_DoesNotRepairReferences()
    {
        var party = CreateParty();
        var removedTarget = CreatePartyWithPartyBase();
        SetPartyTargets(party, removedTarget);

        var objectManager = new Mock<IObjectManager>();
        string missingPartyId = null!;
        string missingTargetId = null!;
        string missingPartyBaseId = null!;
        objectManager.Setup(m => m.TryGetId(party, out missingPartyId)).Returns(false);
        objectManager.Setup(m => m.TryGetId(removedTarget, out missingTargetId)).Returns(false);
        objectManager.Setup(m => m.TryGetId(removedTarget.Party, out missingPartyBaseId)).Returns(false);

        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        bool created = snapshot.TryCreateJoinState(
            party,
            LiveParties(party),
            LiveSettlements(),
            out _,
            out string failure);

        Assert.False(created);
        Assert.Equal("party is not registered", failure);
        Assert.Same(removedTarget.Party, party.Ai.AiBehaviorInteractable);
        Assert.Same(removedTarget, party.TargetParty);
        Assert.Same(removedTarget, party.MoveTargetParty);
    }

    private static MobileParty CreateParty()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Ai = new MobilePartyAi(party);
        party.IsActive = true;
        party.PartyMoveMode = MoveModeType.Party;
        party.MoveTargetPoint = new CampaignVec2(new Vec2(10f, 20f), isOnLand: true);
        return party;
    }

    private static MobileParty CreatePartyWithPartyBase()
    {
        MobileParty party = CreateParty();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party.MobileParty = party;
        return party;
    }

    private static void SetPartyTargets(MobileParty party, MobileParty target)
    {
        target._position = new CampaignVec2(new Vec2(30f, 40f), isOnLand: true);
        party._defaultBehavior = AiBehavior.EngageParty;
        party.ShortTermBehavior = AiBehavior.EngageParty;
        party.Ai.AiBehaviorInteractable = target.Party;
        party.Ai.BehaviorTarget = new CampaignVec2(new Vec2(25f, 35f), isOnLand: true);
        party.TargetParty = target;
        party.MoveTargetParty = target;
        party.DesiredAiNavigationType = MobileParty.NavigationType.Default;
    }

    private static void AssertPartyTargetsUnchanged(MobileParty party, MobileParty target)
    {
        Assert.Equal(AiBehavior.EngageParty, party.DefaultBehavior);
        Assert.Equal(AiBehavior.EngageParty, party.ShortTermBehavior);
        Assert.Same(target.Party, party.Ai.AiBehaviorInteractable);
        Assert.Equal(new CampaignVec2(new Vec2(25f, 35f), isOnLand: true), party.Ai.BehaviorTarget);
        Assert.Same(target, party.TargetParty);
        Assert.Same(target, party.MoveTargetParty);
        Assert.Equal(MoveModeType.Party, party.PartyMoveMode);
        Assert.Equal(new CampaignVec2(new Vec2(10f, 20f), isOnLand: true), party.MoveTargetPoint);
        Assert.Equal(MobileParty.NavigationType.Default, party.DesiredAiNavigationType);
    }

    private static void AssertPointSnapshotWithoutTargets(
        MobilePartyJoinState state,
        CampaignVec2 expectedMoveTargetPoint,
        MobileParty.NavigationType expectedNavigationType)
    {
        Assert.Equal(AiBehavior.GoToPoint, state.Behavior.DefaultBehavior);
        Assert.Equal(AiBehavior.GoToPoint, state.Behavior.NewAiBehavior);
        Assert.Null(state.Behavior.InteractablePointId);
        Assert.Null(state.Behavior.TargetPartyId);
        Assert.Null(state.Behavior.TargetSettlementId);
        Assert.Null(state.Behavior.MoveTargetPartyId);
        Assert.Equal(MoveModeType.Point, state.Behavior.PartyMoveMode);
        Assert.Equal(expectedMoveTargetPoint, state.Behavior.MoveTargetPoint);
        Assert.Equal(expectedMoveTargetPoint, state.Behavior.BestTargetPoint);
        Assert.Equal(expectedMoveTargetPoint, state.Behavior.TargetPosition);
        Assert.Equal(expectedNavigationType, state.Behavior.DesiredAiNavigationType);
    }

    private static void AssertHeldSnapshotWithoutTargets(
        MobilePartyJoinState state,
        CampaignVec2 expectedMoveTargetPoint)
    {
        Assert.Equal(AiBehavior.Hold, state.Behavior.DefaultBehavior);
        Assert.Equal(AiBehavior.Hold, state.Behavior.NewAiBehavior);
        Assert.Null(state.Behavior.InteractablePointId);
        Assert.Null(state.Behavior.TargetPartyId);
        Assert.Null(state.Behavior.TargetSettlementId);
        Assert.Null(state.Behavior.MoveTargetPartyId);
        Assert.Equal(MoveModeType.Hold, state.Behavior.PartyMoveMode);
        Assert.Equal(expectedMoveTargetPoint, state.Behavior.MoveTargetPoint);
        Assert.Equal(MobileParty.NavigationType.None, state.Behavior.DesiredAiNavigationType);
    }

    private static bool TryCreateJoinStateWithCampaign(
        MobilePartyBehaviorSnapshot snapshot,
        MobileParty party,
        ISet<MobileParty> liveParties,
        ISet<Settlement> liveSettlements,
        out MobilePartyJoinState state,
        out string failure)
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
            return snapshot.TryCreateJoinState(
                party,
                liveParties,
                liveSettlements,
                out state,
                out failure);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }

    private static HashSet<MobileParty> LiveParties(params MobileParty[] parties) => new(parties);

    private static HashSet<Settlement> LiveSettlements(params Settlement[] settlements) => new(settlements);
}
