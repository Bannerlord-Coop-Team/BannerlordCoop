using Common.Util;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.ObjectManager;
using Moq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

[Collection("CampaignCurrentCollection")]
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
    public void TryCreateJoinState_MissingAi_ReportsFailure()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var snapshot = new MobilePartyBehaviorSnapshot(Mock.Of<IObjectManager>());

        bool created = snapshot.TryCreateJoinState(party, out _, out string failure);

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
            var campaignObjectManager = new CampaignObjectManager
            {
                Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
            };
            campaign.CampaignObjectManager = campaignObjectManager;
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

    private static MobileParty CreateParty()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Ai = new MobilePartyAi(party);
        party.PartyMoveMode = MoveModeType.Party;
        party.MoveTargetPoint = new CampaignVec2(new Vec2(10f, 20f), isOnLand: true);
        return party;
    }
}
