#if DEBUG
using Missions.Naval;
using System;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalCopySteeringTests
{
    private const float Dt = 1f / 60f;

    private static CopySteeringKnobs Knobs(params string[] knobs)
    {
        var result = new CopySteeringKnobs();
        foreach (var knob in knobs)
        {
            int separator = knob.IndexOf('=');
            Assert.Null(result.TryApply(knob.Substring(0, separator), knob.Substring(separator + 1)));
        }

        return result;
    }

    // An owner sailing along +x at the given speed, sampled every 50 ms up to time 1 s.
    private static CopySteering Sailing(CopySteeringKnobs knobs, float speed, float yaw = 0f)
    {
        var steering = new CopySteering(knobs);
        for (int index = 0; index <= 20; index++)
        {
            float time = index * 0.05f;
            steering.ObserveOwner(time, new Vec3(speed * time, 0f, 0f), yaw);
        }

        return steering;
    }

    [Fact]
    public void OwnerVelocity_IsMeasuredFromTheOwnerFrames()
    {
        var steering = Sailing(new CopySteeringKnobs(), 5f);

        Assert.Equal(5f, steering.OwnerVelocity.x, 3);
        Assert.Equal(0f, steering.OwnerVelocity.y, 3);
        Assert.Equal(0f, steering.OwnerYawRate, 3);
    }

    [Fact]
    public void Compute_InsideTheDeadZoneAppliesNothing()
    {
        var steering = Sailing(Knobs("deadzone=0.5", "yawdeadzone=2"), 5f);
        var target = steering.TargetOrigin;

        var step = steering.Compute(Dt, target - new Vec3(0.4f, 0f, 0f), 1f * CopySteering.DegToRad, new Vec3(3f, 0f, 0f), 0f);

        Assert.False(step.Snap);
        Assert.Equal(0f, step.DeltaVelocity.Length);
        Assert.Equal(0f, step.DeltaYawRate);
    }

    [Fact]
    public void Compute_OnTargetAtTheOwnerSpeedAppliesNothingOutsideTheDeadZoneEither()
    {
        var steering = Sailing(Knobs("deadzone=0", "tau=0.01"), 5f);

        var step = steering.Compute(Dt, steering.TargetOrigin - new Vec3(0.001f, 0f, 0f), 0f, new Vec3(5f, 0f, 0f), 0f);

        Assert.Equal(0f, step.DeltaVelocity.x, 1);
    }

    [Fact]
    public void Compute_ClampsTheCorrectionSpeed()
    {
        // tau at the step length blends the whole difference in one step, so the result is the clamped target itself.
        var steering = Sailing(Knobs("clamp=2", "tau=0.01", "deadzone=0"), 0f);

        var step = steering.Compute(Dt, steering.TargetOrigin - new Vec3(10f, 0f, 0f), 0f, Vec3.Zero, 0f);

        Assert.Equal(2f, step.DeltaVelocity.x, 3);
        Assert.Equal(0f, step.DeltaVelocity.z);
    }

    [Fact]
    public void Compute_AddsTheOwnerVelocityAndBlendsWithTau()
    {
        var steering = Sailing(Knobs("gain=1", "window=1", "clamp=10", "tau=0.2", "deadzone=0"), 5f);

        var step = steering.Compute(0.05f, steering.TargetOrigin - new Vec3(1f, 0f, 0f), 0f, new Vec3(4f, 0f, 0f), 0f);

        // Target 5 + 1 = 6 m/s against 4 m/s, blended by 0.05 / 0.2.
        Assert.Equal(0.5f, step.DeltaVelocity.x, 3);
        Assert.Equal(0.5f, step.AlongHeading, 3);
    }

    [Fact]
    public void Compute_DropsTheOwnerVelocityOnceTheOwnerFramesStop()
    {
        var steering = Sailing(Knobs("gain=1", "window=1", "clamp=10", "tau=0.01", "deadzone=0"), 5f);
        var behind = steering.TargetOrigin - new Vec3(1f, 0f, 0f);

        // Within MinVelocitySpan of the last owner frame the owner's 5 m/s is still added to the 1 m/s correction.
        Assert.Equal(6f, steering.Compute(0.05f, behind, 0f, Vec3.Zero, 0f).DeltaVelocity.x, 3);
        Assert.False(steering.IsOwnerStale);

        var stale = steering.Compute(0.06f, behind, 0f, Vec3.Zero, 0f);

        Assert.True(steering.IsOwnerStale);
        Assert.Equal(1f, stale.DeltaVelocity.x, 3);
        Assert.Equal(0f, steering.FeedForwardVelocity.Length);

        steering.ObserveOwner(1.2f, steering.TargetOrigin + new Vec3(1f, 0f, 0f), 0f);
        Assert.False(steering.IsOwnerStale);
    }

    [Fact]
    public void Compute_ClampsTheYawCorrection()
    {
        var steering = Sailing(Knobs("yawclamp=5", "tau=0.01", "yawdeadzone=0"), 0f, yaw: 0.5f);

        var step = steering.Compute(Dt, steering.TargetOrigin, 0f, Vec3.Zero, 0f);

        Assert.Equal(5f * CopySteering.DegToRad, step.DeltaYawRate, 4);
    }

    [Fact]
    public void Compute_SnapsBeyondTheSnapDistance()
    {
        var steering = Sailing(Knobs("snap=10"), 0f);

        var step = steering.Compute(Dt, steering.TargetOrigin + new Vec3(0f, 11f, 0f), 0f, Vec3.Zero, 0f);

        Assert.True(step.Snap);
        Assert.Equal(1, steering.Snaps);
    }

    [Fact]
    public void Compute_CountsSignChangesAlongTheHeadingAsOscillations()
    {
        var steering = Sailing(Knobs("deadzone=0", "tau=0.01"), 0f);
        var target = steering.TargetOrigin;

        steering.Compute(Dt, target - new Vec3(1f, 0f, 0f), 0f, Vec3.Zero, 0f);
        steering.Compute(Dt, target + new Vec3(1f, 0f, 0f), 0f, Vec3.Zero, 0f);
        steering.Compute(Dt, target - new Vec3(1f, 0f, 0f), 0f, Vec3.Zero, 0f);
        // A sideways error has no component along the heading, so it keeps the last sign.
        steering.Compute(Dt, target - new Vec3(0f, 1f, 0f), 0f, Vec3.Zero, 0f);
        steering.Compute(Dt, target - new Vec3(1f, 0f, 0f), 0f, Vec3.Zero, 0f);

        Assert.Equal(2, steering.Oscillations);
    }

    [Fact]
    public void Compute_AConvergingApproachDoesNotOscillate()
    {
        var steering = Sailing(Knobs("deadzone=0.1"), 0f);
        var target = steering.TargetOrigin;

        for (float gap = 3f; gap > 0.05f; gap *= 0.8f)
            steering.Compute(Dt, target - new Vec3(gap, 0f, 0f), 0f, Vec3.Zero, 0f);

        Assert.Equal(0, steering.Oscillations);
    }

    [Theory]
    [InlineData(4f, 4f - (2f * (float)Math.PI))]
    [InlineData(-4f, -4f + (2f * (float)Math.PI))]
    [InlineData(1f, 1f)]
    public void WrapAngle_KeepsTheShortestTurn(float radians, float expected)
    {
        Assert.Equal(expected, CopySteering.WrapAngle(radians), 4);
    }

    [Fact]
    public void Compute_WithoutAnOwnerFrameDoesNothing()
    {
        var step = new CopySteering(new CopySteeringKnobs()).Compute(Dt, Vec3.Zero, 0f, Vec3.Zero, 0f);

        Assert.False(step.Snap);
        Assert.Equal(0f, step.DeltaVelocity.Length);
    }
}
#endif
