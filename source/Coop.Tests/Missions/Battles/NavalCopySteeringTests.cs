#if DEBUG
using Missions.Naval;
using System;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalCopySteeringTests
{
    private const float Dt = 1f / 60f;

    internal static CopySteeringKnobs Knobs(params string[] knobs)
    {
        var result = new CopySteeringKnobs();
        foreach (var knob in knobs)
        {
            int separator = knob.IndexOf('=');
            Assert.Null(result.TryApply(knob.Substring(0, separator), knob.Substring(separator + 1)));
        }

        return result;
    }

    // A level frame whose forward axis points at the given yaw, counter-clockwise from +x as CopySteering.YawOf reads it.
    internal static MatrixFrame Frame(float x, float y, float yaw)
    {
        var forward = new Vec3((float)Math.Cos(yaw), (float)Math.Sin(yaw), 0f);
        var side = new Vec3(forward.y, -forward.x, 0f);
        return new MatrixFrame(new Mat3(side, forward, new Vec3(0f, 0f, 1f)), new Vec3(x, y, 0f));
    }

    // A steering that just took one sample at (x, 0) heading +x with the given velocity.
    private static CopySteering Observed(CopySteeringKnobs knobs, float x, Vec2 velocity, float yawRate = 0f)
    {
        var steering = new CopySteering(knobs);
        steering.ObserveOwner(Frame(x, 0f, 0f), velocity, yawRate);
        return steering;
    }

    [Fact]
    public void Target_LeadsTheSampleByItsAgePlusTheLead_UpToTheExtrapolationCap()
    {
        var steering = Observed(Knobs("lead=0.1"), 10f, new Vec2(5f, 0f));

        steering.Compute(0.05f, new Vec3(10f, 0f, 0f), 0f, new Vec2(5f, 0f), 0f);
        float early = steering.Target.Position.x;
        for (int step = 0; step < 60; step++) steering.Compute(Dt, new Vec3(10f, 0f, 0f), 0f, new Vec2(5f, 0f), 0f);

        Assert.Equal(10f + (5f * (0.05f + 0.1f)), early, 3);
        Assert.Equal(10f + (5f * (CopySteering.MaxExtrapolationSeconds + 0.1f)), steering.Target.Position.x, 3);
    }

    [Fact]
    public void Target_ExtrapolatesTheYawByTheOwnersYawRate()
    {
        var steering = Observed(Knobs("lead=0.1"), 0f, Vec2.Zero, yawRate: 0.2f);

        steering.Compute(0.1f, Vec3.Zero, 0f, Vec2.Zero, 0.2f);

        Assert.Equal(0.2f * (0.1f + 0.1f), steering.Target.Yaw, 4);
    }

    [Fact]
    public void Compute_OnTargetAtTheOwnersVelocity_ChangesNothing()
    {
        var steering = Observed(Knobs("lead=0"), 0f, new Vec2(5f, 0f));

        var step = steering.Compute(Dt, new Vec3(5f * Dt, 0f, 0f), 0f, new Vec2(5f, 0f), 0f);

        Assert.Equal(0f, step.DeltaVelocity.Length, 4);
        Assert.Equal(0f, step.DeltaYawRate, 4);
    }

    [Fact]
    public void Compute_AddsTheOwnersVelocityAndBlendsWithTau()
    {
        // 2 m behind: gain 1 / window 1 gives 2 m/s on top of the owner's 5 m/s, against the copy's 4 m/s, blended by 0.05 / 0.2.
        var steering = Observed(Knobs("gain=1", "window=1", "clamp=10", "tau=0.2", "lead=0", "softzone=0.5"), 0f, new Vec2(5f, 0f));

        var step = steering.Compute(0.05f, new Vec3((5f * 0.05f) - 2f, 0f, 0f), 0f, new Vec2(4f, 0f), 0f);

        Assert.Equal(0.75f, step.DeltaVelocity.x, 3);
        Assert.Equal(2f, step.ErrorAlongHeading, 3);
    }

    [Fact]
    public void Compute_ClampsTheCorrectionSpeed()
    {
        var steering = Observed(Knobs("clamp=2", "tau=0.01", "lead=0"), 0f, Vec2.Zero);

        var step = steering.Compute(Dt, new Vec3(-10f, 0f, 0f), 0f, Vec2.Zero, 0f);

        Assert.Equal(2f, step.DeltaVelocity.x, 3);
    }

    // F4: a hard dead zone switched the correction on and off at its edge; the soft zone ramps it in without a step.
    [Fact]
    public void Compute_TheSoftZoneRampsTheCorrectionContinuouslyFromZero()
    {
        float previous = 0f;
        float largestStep = 0f;
        for (int index = 1; index <= 200; index++)
        {
            float gap = index * 0.01f;
            var steering = Observed(Knobs("softzone=0.5", "tau=0.01", "lead=0", "clamp=30"), 0f, Vec2.Zero);
            float correction = steering.Compute(Dt, new Vec3(-gap, 0f, 0f), 0f, Vec2.Zero, 0f).DeltaVelocity.x;

            Assert.True(correction >= previous, $"correction fell at {gap} m");
            largestStep = Math.Max(largestStep, correction - previous);
            previous = correction;
        }

        var nearlyOn = Observed(Knobs("softzone=0.5", "tau=0.01", "lead=0"), 0f, Vec2.Zero);
        Assert.True(nearlyOn.Compute(Dt, new Vec3(-0.01f, 0f, 0f), 0f, Vec2.Zero, 0f).DeltaVelocity.x < 0.001f);
        Assert.True(largestStep < 0.05f, $"largest step {largestStep} m/s per cm");
    }

    [Fact]
    public void Compute_TheYawCorrectionRampsInFromOneDegreeAndIsClamped()
    {
        var small = Observed(Knobs("tau=0.01"), 0f, Vec2.Zero);
        var large = Observed(Knobs("tau=0.01"), 0f, Vec2.Zero);

        var inside = small.Compute(Dt, Vec3.Zero, -0.5f * CopySteering.DegToRad, Vec2.Zero, 0f);
        var outside = large.Compute(Dt, Vec3.Zero, -30f * CopySteering.DegToRad, Vec2.Zero, 0f);

        Assert.Equal(0f, inside.DeltaYawRate, 5);
        Assert.Equal(CopySteering.YawClampDegreesPerSecond * CopySteering.DegToRad, outside.DeltaYawRate, 4);
    }

    [Fact]
    public void Compute_SnapsBeyondTheSnapDistance()
    {
        var steering = Observed(Knobs("snap=10", "lead=0"), 0f, Vec2.Zero);

        var step = steering.Compute(Dt, new Vec3(0f, 11f, 0f), 0f, Vec2.Zero, 0f);

        Assert.True(step.Snap);
        Assert.True(steering.IsSnapQueued);
        Assert.Equal(0f, steering.TakeSnapFrame().origin.y, 3);
    }

    // M10 fix C5: the snap frame takes the extrapolated heading with the extrapolated position, not the sample's heading.
    [Fact]
    public void Compute_SnapsToTheExtrapolatedPositionAndHeading()
    {
        var steering = Observed(Knobs("snap=10", "lead=0.1"), 0f, new Vec2(5f, 0f), yawRate: 0.5f);

        var step = steering.Compute(0.1f, new Vec3(0f, 20f, 0f), 0f, Vec2.Zero, 0f);

        var frame = steering.TakeSnapFrame();
        Assert.True(step.Snap);
        Assert.Equal(steering.Target.Position.x, frame.origin.x, 3);
        Assert.Equal(steering.Target.Yaw, CopySteering.YawOf(frame.rotation.f), 3);
        Assert.Equal(0.5f, steering.FeedForwardYawRate);
    }

    // M10 round 3: a snap queued in a physics step is applied on the mission tick, by when a newer sample may have arrived.
    [Fact]
    public void TakeSnapFrame_UsesTheLatestSampleAndCoversAJumpSinceTheQueue()
    {
        var steering = Observed(Knobs("snap=10", "lead=0"), 0f, Vec2.Zero);
        Assert.True(steering.Compute(Dt, new Vec3(0f, 20f, 0f), 0f, Vec2.Zero, 0f).Snap);

        steering.ObserveOwner(Frame(40f, 0f, 0f), Vec2.Zero, 0f);
        var frame = steering.TakeSnapFrame();
        var next = steering.Compute(Dt, frame.origin, 0f, Vec2.Zero, 0f);

        Assert.Equal(40f, frame.origin.x, 3);
        Assert.False(next.Snap);
        Assert.False(steering.IsSnapQueued);
    }

    [Fact]
    public void RotateYaw_TurnsEveryAxisAboutWorldUpAndKeepsThemOrthonormal()
    {
        var rotation = Frame(0f, 0f, 0.3f).rotation;

        var turned = CopySteering.RotateYaw(rotation, 0.4f);

        Assert.Equal(0.7f, CopySteering.YawOf(turned.f), 4);
        Assert.Equal(1f, turned.u.z, 4);
        Assert.Equal(0f, Vec3.DotProduct(turned.s, turned.f), 4);
        Assert.Equal(1f, turned.s.Length, 4);
    }

    [Fact]
    public void Compute_SnapsOnceWhenTheOwnerTeleports()
    {
        var steering = Observed(Knobs("snap=25", "lead=0"), 0f, new Vec2(5f, 0f));
        steering.Compute(0.05f, new Vec3(0.25f, 0f, 0f), 0f, new Vec2(5f, 0f), 0f);

        // The copy sits near the new sample already, so only the jump between samples can snap it.
        steering.ObserveOwner(Frame(40f, 0f, 0f), new Vec2(5f, 0f), 0f);
        bool queuedByTheJump = steering.IsSnapQueued;
        var jump = steering.Compute(Dt, new Vec3(40f, 0f, 0f), 0f, new Vec2(5f, 0f), 0f);
        var frame = steering.TakeSnapFrame();
        var after = steering.Compute(Dt, new Vec3(40f, 0f, 0f), 0f, new Vec2(5f, 0f), 0f);

        Assert.True(queuedByTheJump);
        Assert.True(jump.Snap);
        Assert.InRange(frame.origin.x, 40f, 40.2f);
        Assert.False(after.Snap);
        Assert.Equal(5f, steering.FeedForwardVelocity.x);
    }

    [Fact]
    public void Compute_DropsTheOwnersVelocityOnceItsSamplesAreStale()
    {
        var steering = Observed(Knobs("tau=0.01", "lead=0"), 0f, new Vec2(5f, 0f));

        var fresh = steering.Compute(0.5f, new Vec3(5f * 0.3f, 0f, 0f), 0f, Vec2.Zero, 0f);
        Assert.False(steering.IsOwnerStale);
        var stale = steering.Compute(0.6f, new Vec3(5f * 0.3f, 0f, 0f), 0f, Vec2.Zero, 0f);

        Assert.Equal(5f, fresh.DeltaVelocity.x, 3);
        Assert.True(steering.IsOwnerStale);
        Assert.Equal(0f, stale.DeltaVelocity.x, 3);
        Assert.Equal(0f, steering.FeedForwardVelocity.Length);
        steering.ObserveOwner(Frame(2f, 0f, 0f), new Vec2(5f, 0f), 0f);
        Assert.False(steering.IsOwnerStale);
    }

    // R41b F4: an owner sampled at 20 Hz with jittered arrival, followed straight and through a constant-rate turn.
    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    public void Compute_FollowsJittered20HzSamplesWithoutOscillating(float yawRate)
    {
        var owner = new SimulatedOwner(speed: 5f, yawRate);
        var copy = new SimulatedCopy(owner.PositionAt(0f), owner.VelocityAt(0f), 0f, yawRate);
        // The lead covers the modelled 80 ms transit, which the sample's age on this clock misses.
        var steering = new CopySteering(Knobs("lead=0.08"));
        var diagnostics = new CopyDiagnostics();
        var random = new Random(41);
        const float latency = 0.08f;
        int nextSample = 0;
        float nextArrival = latency;
        float driftSum = 0f;
        int driftSamples = 0;

        for (float time = 0f; time < 20f; time += Dt)
        {
            while (nextArrival <= time)
            {
                float sentAt = nextSample * BattleShipSendInterval;
                steering.ObserveOwner(owner.FrameAt(sentAt), owner.VelocityAt(sentAt), yawRate);
                nextSample++;
                float jitter = ((float)random.NextDouble() - 0.5f) * 0.04f;
                nextArrival = Math.Max(nextArrival, (nextSample * BattleShipSendInterval) + latency + jitter);
            }

            copy.Step(Steer(steering, diagnostics, copy), Dt);
            if (time < 2f) continue;

            driftSum += (copy.Origin.AsVec2 - owner.PositionAt(time)).Length;
            driftSamples++;
        }

        Assert.Equal(0, diagnostics.Oscillations);
        Assert.False(steering.IsSnapQueued);
        // Inside the soft zone; a live copy's replayed rudder also turns its velocity, which this model leaves to the steering.
        Assert.True(driftSum / driftSamples < 0.4f, $"mean drift {driftSum / driftSamples} m");
    }

    private const float BattleShipSendInterval = 0.05f;

    // One steering step from the modelled copy's state, recorded the way NavalSteeredCopies records it (default tau).
    private static CopySteeringStep Steer(CopySteering steering, CopyDiagnostics diagnostics, SimulatedCopy copy)
    {
        var step = steering.Compute(Dt, copy.Origin, copy.Yaw, copy.Velocity, copy.YawRate);
        diagnostics.RecordStep(0f, Dt, new CopySteeringKnobs().TauSeconds, step);
        return step;
    }

    [Fact]
    public void Knobs_DefaultToNoLead()
    {
        Assert.Equal(0f, new CopySteeringKnobs().LeadSeconds);
    }

    // R42: with no transit delay the sample's age already places the target on the owner; a lead puts the copy speed x lead ahead.
    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    public void Compute_JitteredLanSamplesAtConstantVelocity_RunAheadOnlyByTheLead(float lead)
    {
        const float speed = 9f;
        var owner = new SimulatedOwner(speed, 0f);
        var copy = new SimulatedCopy(owner.PositionAt(0f), owner.VelocityAt(0f), 0f, 0f);
        var steering = new CopySteering(Knobs(FormattableString.Invariant($"lead={lead}")));
        var diagnostics = new CopyDiagnostics();
        var random = new Random(42);
        int nextSample = 0;
        float nextArrival = 0f;
        float alongSum = 0f;
        int samples = 0;

        for (int tick = 0; tick < 20 * 60; tick++)
        {
            float time = tick * Dt;
            while (nextArrival <= time)
            {
                float sentAt = nextSample * BattleShipSendInterval;
                steering.ObserveOwner(owner.FrameAt(sentAt), owner.VelocityAt(sentAt), 0f);
                nextSample++;
                // A LAN: arrival 0 to 10 ms after sending, never before.
                nextArrival = Math.Max(nextArrival, (nextSample * BattleShipSendInterval) + ((float)random.NextDouble() * 0.01f));
            }

            copy.Step(Steer(steering, diagnostics, copy), Dt);
            if (time < 5f) continue;

            // The step moved the copy to the end of this tick.
            alongSum += copy.Origin.x - owner.PositionAt(time + Dt).x;
            samples++;
        }

        float alongMean = alongSum / samples;
        Assert.True(Math.Abs(alongMean - (speed * lead)) < 0.1f, $"along-track mean {alongMean} m, lead {lead} s");
        Assert.Equal(0, diagnostics.Oscillations);
    }

    // R42: the probe measures drift against where the owner is, so a tuned lead doesn't count as drift.
    [Fact]
    public void OwnerEstimate_ExtrapolatesTheSampleByItsAgeWithoutTheLead()
    {
        var steering = Observed(Knobs("lead=0.3"), 10f, new Vec2(5f, 0f), yawRate: 0.2f);

        steering.Compute(0.1f, new Vec3(10f, 0f, 0f), 0f, new Vec2(5f, 0f), 0.2f);
        var early = steering.OwnerEstimate;
        float earlyTarget = steering.Target.Position.x;
        steering.Compute(1f, new Vec3(10f, 0f, 0f), 0f, new Vec2(5f, 0f), 0.2f);

        Assert.Equal(10f + (5f * 0.1f), early.Position.x, 3);
        Assert.Equal(0.2f * 0.1f, early.Yaw, 4);
        Assert.Equal(10f + (5f * (0.1f + 0.3f)), earlyTarget, 3);
        Assert.Equal(10f + (5f * CopySteering.MaxExtrapolationSeconds), steering.OwnerEstimate.Position.x, 3);
    }

    [Fact]
    public void Knobs_AreSharedSoATunedKnobReachesEveryExistingSteering()
    {
        var knobs = new CopySteeringKnobs();
        var steering = Observed(knobs, 0f, Vec2.Zero);

        Assert.Null(knobs.TryApply("snap", "5"));
        var step = steering.Compute(Dt, new Vec3(6f, 0f, 0f), 0f, Vec2.Zero, 0f);

        Assert.True(step.Snap);
    }

    [Theory]
    [InlineData("deadzone", "1", "Unknown knob")]
    [InlineData("lead", "2", "lead (s) must be")]
    [InlineData("softzone", "x", "softzone (m) must be")]
    public void Knobs_RefuseUnknownOrOutOfRangeValues(string key, string value, string expected)
    {
        Assert.StartsWith(expected, new CopySteeringKnobs().TryApply(key, value));
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
    public void Compute_WithoutASampleDoesNothing()
    {
        var step = new CopySteering(new CopySteeringKnobs()).Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f);

        Assert.False(step.Snap);
        Assert.Equal(0f, step.DeltaVelocity.Length);
    }

    // An owner sailing at a constant speed from the origin along +x, turning at a constant yaw rate.
    private sealed class SimulatedOwner
    {
        private readonly float speed;
        private readonly float yawRate;

        public SimulatedOwner(float speed, float yawRate)
        {
            this.speed = speed;
            this.yawRate = yawRate;
        }

        public Vec2 PositionAt(float time)
        {
            if (yawRate == 0f) return new Vec2(speed * time, 0f);

            float radius = speed / yawRate;
            float yaw = yawRate * time;
            return new Vec2(radius * (float)Math.Sin(yaw), radius * (1f - (float)Math.Cos(yaw)));
        }

        public Vec2 VelocityAt(float time)
        {
            float yaw = yawRate * time;
            return new Vec2(speed * (float)Math.Cos(yaw), speed * (float)Math.Sin(yaw));
        }

        public MatrixFrame FrameAt(float time)
        {
            var position = PositionAt(time);
            return Frame(position.x, position.y, yawRate * time);
        }
    }

    // A copy body that only moves by the steering's velocity changes, the harshest case for the feed-forward.
    private sealed class SimulatedCopy
    {
        public SimulatedCopy(Vec2 position, Vec2 velocity, float yaw, float yawRate)
        {
            Origin = new Vec3(position.x, position.y, 0f);
            Velocity = velocity;
            Yaw = yaw;
            YawRate = yawRate;
        }

        public Vec3 Origin { get; private set; }
        public Vec2 Velocity { get; private set; }
        public float Yaw { get; private set; }
        public float YawRate { get; private set; }

        public void Step(CopySteeringStep step, float dt)
        {
            Velocity += step.DeltaVelocity;
            YawRate += step.DeltaYawRate;
            Origin += new Vec3(Velocity.x * dt, Velocity.y * dt, 0f);
            Yaw += YawRate * dt;
        }
    }
}
#endif
