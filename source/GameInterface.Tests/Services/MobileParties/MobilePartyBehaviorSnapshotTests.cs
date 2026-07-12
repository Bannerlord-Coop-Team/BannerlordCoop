using Common.Util;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Verifies complete mobile-party behavior snapshot creation.
/// </summary>
public class MobilePartyBehaviorSnapshotTests
{
    [Fact]
    public void TryCreate_AnchorPoint_UsesOwnerPartyReference()
    {
        const string ownerId = "MobileParty_anchor-owner";
        var owner = ObjectHelper.SkipConstructor<MobileParty>();
        var anchor = new AnchorPoint(owner);
        var objectManager = new Mock<IObjectManager>();
        string resolvedOwnerId = ownerId;
        objectManager
            .Setup(manager => manager.TryGetId(owner, out resolvedOwnerId))
            .Returns(true);

        var behaviorTarget = new CampaignVec2(new Vec2(0.25f, 0.5f), true);
        var moveTarget = new CampaignVec2(new Vec2(0.75f, 0.5f), true);

        Assert.True(MobilePartyBehaviorSnapshot.TryCreate(
            objectManager.Object,
            owner,
            AiBehavior.GoToPoint,
            anchor,
            behaviorTarget,
            moveTarget,
            out var data));

        Assert.True(data.HasTarget);
        Assert.Equal(BehaviorInteractableKind.AnchorPoint, data.InteractableKind);
        Assert.Equal(
            global::GameInterface.Services.ObjectManager.ObjectManager.Compact(ownerId, typeof(MobileParty)),
            data.InteractablePointId);
        Assert.Equal(behaviorTarget, data.BestTargetPoint);
        Assert.Equal(moveTarget, data.MoveTargetPoint);
    }
}
