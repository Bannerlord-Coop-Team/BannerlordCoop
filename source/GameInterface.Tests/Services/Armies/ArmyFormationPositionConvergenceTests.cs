using GameInterface.Services.Armies;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Armies;

/// <summary>Tests formation-only client position reporting and authoritative application gates.</summary>
public class ArmyFormationPositionConvergenceTests
{
    private readonly ArmyFormationPositionConvergence convergence = new ArmyFormationPositionConvergence();

    [Fact]
    public void MovingPlayerLeaderWithSummonedCompanion_ReportsAfterMovementThreshold()
    {
        CampaignVec2 previous = Position(10f, 10f);
        var state = EligibleState(Position(10.3f, 10f));

        Assert.True(convergence.ShouldReport(
            state,
            hasPreviousPosition: true,
            previous));
    }

    [Fact]
    public void StationaryPlayerLeader_ReportsWhenCompanionStartsConverging()
    {
        CampaignVec2 position = Position(10f, 10f);
        var state = EligibleState(position);

        Assert.True(convergence.ShouldReport(
            state,
            hasPreviousPosition: false,
            previousPosition: default));
    }

    [Fact]
    public void DistantCompanion_PositionUpdateDoesNotSatisfyVanillaAttachmentDistance()
    {
        CampaignVec2 reportedLeaderPosition = Position(12f, 12f);
        CampaignVec2 distantCompanionPosition = Position(20f, 20f);
        var state = State(
            Position(11f, 12f),
            isArmyLeader: true,
            hasConvergingMember: true,
            hasNearbyConvergingMember: false);

        Assert.False(convergence.ShouldReport(
            state,
            hasPreviousPosition: false,
            previousPosition: default));
        Assert.True(convergence.ShouldApply(state, reportedLeaderPosition));
        Assert.False(distantCompanionPosition.DistanceSquared(reportedLeaderPosition) < 1.5f);
    }

    [Fact]
    public void RepeatedAuthoritativePosition_IsIdempotent()
    {
        CampaignVec2 reportedPosition = Position(12f, 12f);

        Assert.True(convergence.ShouldApply(
            EligibleState(Position(11f, 12f)),
            reportedPosition));
        Assert.False(convergence.ShouldApply(
            EligibleState(reportedPosition),
            reportedPosition));
    }

    [Fact]
    public void SaveRejoinWithoutPreviousSample_ReportsCurrentFormationPosition()
    {
        CampaignVec2 position = Position(12f, 12f);

        Assert.True(convergence.ShouldReport(
            EligibleState(position),
            hasPreviousPosition: false,
            previousPosition: default));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void SeparationOrRemoval_StopsPositionReports(
        bool hasConvergingMember,
        bool isArmyLeader)
    {
        var state = State(
            Position(12f, 12f),
            isArmyLeader,
            hasConvergingMember);

        Assert.False(convergence.ShouldReport(
            state,
            hasPreviousPosition: false,
            previousPosition: default));
    }

    [Fact]
    public void SmallMovement_DoesNotFloodPositionReports()
    {
        CampaignVec2 previous = Position(10f, 10f);
        var state = EligibleState(Position(10.1f, 10f));

        Assert.False(convergence.ShouldReport(
            state,
            hasPreviousPosition: true,
            previous));
    }

    private static ArmyFormationPositionState EligibleState(CampaignVec2 position) =>
        State(
            position,
            isArmyLeader: true,
            hasConvergingMember: true,
            hasNearbyConvergingMember: true);

    private static ArmyFormationPositionState State(
        CampaignVec2 position,
        bool isArmyLeader,
        bool hasConvergingMember,
        bool? hasNearbyConvergingMember = null) =>
        new ArmyFormationPositionState(
            "player_party",
            position,
            isActive: true,
            isControlled: true,
            isArmyLeader,
            isAttached: false,
            isInMapEvent: false,
            isInSettlement: false,
            isCurrentlyAtSea: false,
            hasConvergingMember,
            hasNearbyConvergingMember ?? hasConvergingMember);

    private static CampaignVec2 Position(float x, float y) =>
        new CampaignVec2(new Vec2(x, y), isOnLand: true);
}
