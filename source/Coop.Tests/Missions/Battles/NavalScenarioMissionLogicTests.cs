#if COOP_NAVAL
using GameInterface.Services.MapEvents.NavalScenarios;
using Missions.Naval;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalScenarioMissionLogicTests
{
    private const float Tolerance = 0.001f;

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    public void IsReadyEnabled_PressesReadyOnlyWhenTheOrderOfBattleButtonWouldAccept(
        bool viewActive, bool assignmentDirty, bool canStartMission, bool expected)
    {
        Assert.Equal(expected, NavalScenarioMissionLogic.IsReadyEnabled(viewActive, assignmentDirty, canStartMission));
    }

    [Fact]
    public void PlacementPose_BoardingLiesParallelBesideTheTargetWithTheSpacingBetweenTheHulls()
    {
        // Target at (100, 50) heading +y; own hull 8 m wide, target 10 m wide, 10 m gap.
        var (position, direction) = NavalScenarioMissionLogic.PlacementPose(NavalScenarioPlacement.Boarding,
            new Vec2(100f, 50f), new Vec2(0f, 2f), new Vec2(8f, 30f), new Vec2(10f, 40f), 10f, starboard: true);

        AssertNear(new Vec2(119f, 50f), position);
        AssertNear(new Vec2(0f, 1f), direction);
    }

    [Fact]
    public void PlacementPose_BoardingOnThePortSideMirrorsTheStarboardPose()
    {
        var (position, direction) = NavalScenarioMissionLogic.PlacementPose(NavalScenarioPlacement.Boarding,
            new Vec2(100f, 50f), new Vec2(0f, 1f), new Vec2(8f, 30f), new Vec2(10f, 40f), 10f, starboard: false);

        AssertNear(new Vec2(81f, 50f), position);
        AssertNear(new Vec2(0f, 1f), direction);
    }

    [Fact]
    public void PlacementPose_RammingPointsTheBowAtTheTargetsSideFromSpacingAway()
    {
        // Own hull 30 m long: its centre sits half a length plus the spacing plus half the target's beam away.
        var (position, direction) = NavalScenarioMissionLogic.PlacementPose(NavalScenarioPlacement.Ramming,
            new Vec2(0f, 0f), new Vec2(1f, 0f), new Vec2(8f, 30f), new Vec2(10f, 40f), 60f, starboard: true);

        AssertNear(new Vec2(0f, -80f), position);
        AssertNear(new Vec2(0f, 1f), direction);
    }

    [Fact]
    public void SinkingHitDamage_TakesMoreThanThePreDamagedHullHasLeftButNotAHealthyHull()
    {
        const float maxHitPoints = 10000f;
        float damage = NavalScenarioMissionLogic.SinkingHitDamage(maxHitPoints);

        Assert.Equal(2f * maxHitPoints * NavalScenarioPresets.SinkingHullHitPointRatio, damage, 3);
        Assert.True(damage < maxHitPoints);
    }

    private static void AssertNear(Vec2 expected, Vec2 actual)
    {
        Assert.InRange(actual.x, expected.x - Tolerance, expected.x + Tolerance);
        Assert.InRange(actual.y, expected.y - Tolerance, expected.y + Tolerance);
    }
}
#endif
