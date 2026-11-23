using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Coop.IntegrationTests.MobileParties;

/// <summary>
/// Pure checks on <see cref="MobilePartyBehaviorHandler"/> statics; no test environment needed.
/// </summary>
public class MobilePartyBehaviorHandlerStaticTests
{
    [Fact]
    public void TrySelectBehaviorUpdate_SelfEchoUsesLatestPrediction()
    {
        var stalePoint = new CampaignVec2(new Vec2(12f, 8f), true);
        var latestPoint = new CampaignVec2(new Vec2(42f, 24f), true);
        var staleEcho = new PartyBehaviorUpdateData(
            "MobileParty_Test_Party",
            AiBehavior.EscortParty,
            "Old_Target",
            stalePoint,
            stalePoint,
            default,
            stalePoint,
            default);
        var latestPrediction = new PartyBehaviorUpdateData(
            "Test_Party",
            AiBehavior.GoToPoint,
            null,
            latestPoint,
            latestPoint,
            default,
            latestPoint,
            default);
        var latestPredictions = new Dictionary<string, PartyBehaviorUpdateData>
        {
            ["Test_Party"] = latestPrediction,
        };

        Assert.True(MobilePartyBehaviorHandler.TrySelectBehaviorUpdate(
            true,
            latestPredictions,
            ref staleEcho));
        Assert.Equal(AiBehavior.GoToPoint, staleEcho.NewAiBehavior);
        Assert.Null(staleEcho.InteractablePointId);
        Assert.Equal(latestPoint.X, staleEcho.BestTargetPoint.X);
        Assert.Equal(latestPoint.Y, staleEcho.BestTargetPoint.Y);
        Assert.Equal(latestPoint.IsOnLand, staleEcho.BestTargetPoint.IsOnLand);
    }

    [Theory]
    [InlineData(false, false, false, true, true, false)]
    [InlineData(false, false, true, true, true, true)]
    [InlineData(false, false, false, true, false, true)]
    [InlineData(false, true, false, true, true, true)]
    [InlineData(true, true, true, true, false, false)]
    public void ShouldApplyAuthoritativePosition_ReturnsExpected(
        bool isSelfEcho,
        bool forcePosition,
        bool isHolding,
        bool currentIsOnLand,
        bool authoritativeIsOnLand,
        bool expected)
    {
        var currentPosition = new CampaignVec2(new Vec2(42f, 24f), currentIsOnLand);
        var authoritativePosition = new CampaignVec2(new Vec2(12f, 8f), authoritativeIsOnLand);

        Assert.Equal(
            expected,
            MobilePartyBehaviorHandler.ShouldApplyAuthoritativePosition(
                isSelfEcho,
                forcePosition,
                isHolding,
                currentPosition,
                authoritativePosition));
    }

    // A raft party ignores the owner's move mode like vanilla does, but a target that only changes IsOnLand is still a new order
    [Theory]
    [InlineData(AiBehavior.GoToPoint, true, MoveModeType.Party, false)]
    [InlineData(AiBehavior.Hold, true, MoveModeType.Point, true)]
    [InlineData(AiBehavior.GoToPoint, false, MoveModeType.Point, true)]
    public void OwnerOrderDiffers_ComparesBehaviorAndTargetOnly(
        AiBehavior ownerBehavior,
        bool ownerTargetIsOnLand,
        MoveModeType ownerMoveMode,
        bool expected)
    {
        var serverTarget = new CampaignVec2(new Vec2(30f, 10f), true);
        var ownerTarget = new CampaignVec2(new Vec2(30f, 10f), ownerTargetIsOnLand);
        var ownerOrder = new PartyBehaviorUpdateData(
            mobilePartyId: "Test_Party",
            newAiBehavior: ownerBehavior,
            interactablePointId: null,
            bestTargetPoint: ownerTarget,
            partyPosition: ownerTarget,
            defaultBehavior: ownerBehavior,
            targetPosition: ownerTarget,
            desiredAiNavigationType: default)
        {
            PartyMoveMode = ownerMoveMode,
        };

        Assert.Equal(expected, MobilePartyBehaviorHandler.OwnerOrderDiffers(ownerOrder, AiBehavior.GoToPoint, serverTarget));
    }
}
