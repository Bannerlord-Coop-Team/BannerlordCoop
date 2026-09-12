using Missions.Missiles.Handlers;
using TaleWorlds.Library;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class MissileReplayPlannerTests
{
    private static readonly Mat3 LaunchOrientation = new Mat3(
        new Vec3(0f, -1f, 0f),
        new Vec3(1f, 0f, 0f),
        new Vec3(0f, 0f, 1f));

    [Fact]
    public void FastForward_EndsAtCurrentVictimPositionRatherThanHistoricalImpact()
    {
        Vec3 currentVictimPosition = new Vec3(100f, 10f, 1f);

        MissileReplayPlan plan = MissileReplayPlanner.Plan(
            new Vec3(0f, 0f, 1f),
            new Vec3(1f, 0f, 0f),
            LaunchOrientation,
            60f,
            fastForward: true,
            currentVictimPosition,
            new Vec3(50f, 0f, 0f));

        Vec3 endpoint = plan.Position + (plan.Direction * (plan.Speed * plan.RemainingFlightSeconds));

        Assert.True(plan.IsFastForwarded);
        AssertVec3(currentVictimPosition, endpoint);
        Assert.InRange(plan.RemainingFlightSeconds, 0f, MissileReplayPlanner.FinalSegmentSeconds);
        AssertVec3(plan.Direction, plan.Orientation.f);
    }

    [Fact]
    public void OrdinaryShot_PreservesOriginalLaunch()
    {
        Vec3 position = new Vec3(1f, 2f, 3f);
        Vec3 direction = new Vec3(1f, 0f, 0f);

        MissileReplayPlan plan = MissileReplayPlanner.Plan(position, direction, LaunchOrientation, 60f,
            fastForward: false, currentTarget: default, impactVelocity: default);

        Assert.False(plan.IsFastForwarded);
        Assert.Equal(position, plan.Position);
        Assert.Equal(direction, plan.Direction);
        Assert.Equal(LaunchOrientation, plan.Orientation);
        Assert.Equal(60f, plan.Speed);
        Assert.Equal(0f, plan.RemainingFlightSeconds);
    }

    [Fact]
    public void ShortRangeFinalSegment_StartsAtOriginalLaunch()
    {
        Vec3 origin = new Vec3(5f, 6f, 1f);
        Vec3 currentVictimPosition = new Vec3(6f, 6f, 1f);

        MissileReplayPlan plan = MissileReplayPlanner.Plan(origin, new Vec3(1f, 0f, 0f),
            LaunchOrientation, 60f, fastForward: true, currentVictimPosition, new Vec3(50f, 0f, 0f));

        Assert.True(plan.IsFastForwarded);
        AssertVec3(origin, plan.Position);
        Assert.Equal(1f / 50f, plan.RemainingFlightSeconds, 4);
        AssertVec3(currentVictimPosition,
            plan.Position + (plan.Direction * (plan.Speed * plan.RemainingFlightSeconds)));
    }

    private static void AssertVec3(Vec3 expected, Vec3 actual)
    {
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }
}
