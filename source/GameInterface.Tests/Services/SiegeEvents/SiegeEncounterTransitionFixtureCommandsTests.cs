using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.SiegeEvents.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class SiegeEncounterTransitionFixtureCommandsTests
{
    [Fact]
    public void BehaviorMatches_AllFieldsRestoredByTryApplyMustMatch()
    {
        PartyBehaviorUpdateData expected = CreateBehavior();
        PartyBehaviorUpdateData actual = CreateBehavior();

        Assert.True(SiegeEncounterTransitionFixtureCommands.BehaviorMatches(expected, actual));

        actual.TargetPartyId = "different-party";
        Assert.False(SiegeEncounterTransitionFixtureCommands.BehaviorMatches(expected, actual));

        actual = CreateBehavior();
        actual.IsInteractableAnchor = !expected.IsInteractableAnchor;
        Assert.False(SiegeEncounterTransitionFixtureCommands.BehaviorMatches(expected, actual));

        actual = CreateBehavior();
        actual.MoveTargetPartyId = "different-move-target";
        Assert.False(SiegeEncounterTransitionFixtureCommands.BehaviorMatches(expected, actual));
    }

    [Fact]
    public void BehaviorMatches_DetectsConstructorFieldDifferences()
    {
        PartyBehaviorUpdateData expected = CreateBehavior();
        PartyBehaviorUpdateData actual = new PartyBehaviorUpdateData(
            "party",
            (AiBehavior)2,
            "different-interactable",
            Point(9f, 9f),
            expected.PartyPosition,
            (AiBehavior)3,
            Point(8f, 8f),
            (MobileParty.NavigationType)2)
        {
            TargetPartyId = expected.TargetPartyId,
            TargetSettlementId = expected.TargetSettlementId,
            MoveTargetPoint = expected.MoveTargetPoint,
            IsTargetingPort = expected.IsTargetingPort,
            PartyMoveMode = expected.PartyMoveMode,
            MoveTargetPartyId = expected.MoveTargetPartyId,
            IsInteractableAnchor = expected.IsInteractableAnchor,
            IsCurrentlyAtSea = expected.IsCurrentlyAtSea,
        };

        Assert.False(SiegeEncounterTransitionFixtureCommands.BehaviorMatches(expected, actual));
    }

    private static PartyBehaviorUpdateData CreateBehavior() =>
        new PartyBehaviorUpdateData(
            "party",
            (AiBehavior)1,
            "interactable",
            Point(1f, 2f),
            Point(3f, 4f),
            (AiBehavior)4,
            Point(5f, 6f),
            (MobileParty.NavigationType)1)
        {
            TargetPartyId = "target-party",
            TargetSettlementId = "target-settlement",
            MoveTargetPoint = Point(7f, 8f),
            IsTargetingPort = true,
            PartyMoveMode = MoveModeType.Point,
            MoveTargetPartyId = "move-target",
            IsInteractableAnchor = true,
            IsCurrentlyAtSea = true,
        };

    private static CampaignVec2 Point(float x, float y) => new(new Vec2(x, y), isOnLand: true);
}
