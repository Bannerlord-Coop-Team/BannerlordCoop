using Common.Util;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Moq;
using ProtoBuf;
using System.Linq;
using System.Reflection;
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
    [Theory]
    [InlineData("GameInterface.Services.MobilePartyAIs.Messages.AiBehaviorInteractablePointUpdated")]
    [InlineData("GameInterface.Services.MobilePartyAIs.Messages.UpdateAiBehaviorInteractablePoint")]
    public void ObsoleteInteractableProtocolTypes_AreNotInAssembly(string fullName)
    {
        Assert.Null(typeof(MobilePartyBehaviorSnapshot).Assembly.GetType(fullName));
    }

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
            global::GameInterface.Services.ObjectManager.ObjectManager.Compact(ownerId, typeof(MobileParty)),
            data.InteractablePointId);
        Assert.Equal(behaviorTarget, data.BestTargetPoint);
        Assert.Equal(moveTarget, data.MoveTargetPoint);
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
