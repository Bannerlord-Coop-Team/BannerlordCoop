using Common.Util;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Moq;
using ProtoBuf;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
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
    public void PartyBehaviorUpdateData_UsesOneCurrentSequentialWireContract()
    {
        var tags = typeof(PartyBehaviorUpdateData)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public)
            .Select(member => member.GetCustomAttribute<ProtoMemberAttribute>())
            .Where(attribute => attribute != null)
            .Select(attribute => attribute.Tag)
            .OrderBy(tag => tag)
            .ToArray();

        Assert.Equal(Enumerable.Range(1, 19).ToArray(), tags);
    }

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
        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        var behaviorTarget = new CampaignVec2(new Vec2(0.25f, 0.5f), true);
        var moveTarget = new CampaignVec2(new Vec2(0.75f, 0.5f), true);

        Assert.True(snapshot.TryCreate(
            owner,
            AiBehavior.GoToPoint,
            anchor,
            behaviorTarget,
            moveTarget,
            out var data));

        Assert.True(data.HasTarget);
        Assert.Equal(BehaviorInteractableKind.AnchorPoint, data.InteractableKind);
        Assert.Equal(
            ObjectManager.Compact(ownerId, typeof(MobileParty)),
            data.InteractablePointId);
        Assert.Equal(behaviorTarget, data.BestTargetPoint);
        Assert.Equal(moveTarget, data.MoveTargetPoint);
    }

    [Fact]
    public void TryCreate_UnsupportedInteractable_ReturnsFalse()
    {
        const string partyId = "MobileParty_test-party";
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var unsupportedInteractable = new Mock<IInteractablePoint>().Object;
        var objectManager = new Mock<IObjectManager>();
        string resolvedPartyId = partyId;
        objectManager
            .Setup(manager => manager.TryGetId(party, out resolvedPartyId))
            .Returns(true);
        var snapshot = new MobilePartyBehaviorSnapshot(objectManager.Object);

        Assert.False(snapshot.TryCreate(
            party,
            AiBehavior.GoToPoint,
            unsupportedInteractable,
            default,
            default,
            out _));
    }

    [Fact]
    public void ShouldPublishMovementState_InactiveAttachedParty_ReturnsFalse()
    {
        var leader = ObjectHelper.SkipConstructor<MobileParty>();
        var attachedParty = ObjectHelper.SkipConstructor<MobileParty>();
        attachedParty._attachedTo = leader;
        attachedParty.IsActive = false;

        Assert.False(MobilePartyMovementStatePatches.ShouldPublishMovementState(
            attachedParty,
            isAuthoritativeMutation: true,
            exception: null));
    }
}
