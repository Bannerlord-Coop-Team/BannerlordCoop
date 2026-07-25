using Common.Util;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

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

    private static MobileParty CreateParty()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Ai = new MobilePartyAi(party);
        party.PartyMoveMode = MoveModeType.Party;
        party.MoveTargetPoint = new CampaignVec2(new Vec2(10f, 20f), isOnLand: true);
        return party;
    }
}
