#if DEBUG
using Missions.Naval;
using System;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalCopyDiagnosticsTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void RollingWindow_KeepsOnlyTheLastTenSeconds()
    {
        var window = new RollingWindow();
        window.Add(0f, 100f);
        window.Add(5f, 1f);
        window.Add(12f, 3f);

        Assert.Equal(2, window.Count);
        Assert.Equal(2f, window.Mean, 4);
        Assert.Equal(3f, window.Max);
    }

    // R43: one 3.4 ms spike stood for the whole window's steer cost; the p99 of 100 frames skips the single worst one.
    [Fact]
    public void RollingWindow_Percentile_IsTheNearestRankValue()
    {
        var window = new RollingWindow();
        for (int frame = 1; frame <= 99; frame++) window.Add(frame * 0.01f, 0.03f);
        window.Add(1f, 3.4f);

        Assert.Equal(0.03f, window.Percentile(99f));
        Assert.Equal(3.4f, window.Percentile(100f));
        Assert.Equal(3.4f, window.Max);
        Assert.Equal(0f, new RollingWindow().Percentile(99f));
    }

    // The probe sums per-frame GC collection deltas over the window.
    [Fact]
    public void RollingWindow_Sum_AddsTheWindowsValues()
    {
        var window = new RollingWindow();
        window.Add(0f, 1f);
        window.Add(1f, 0f);
        window.Add(2f, 2f);

        Assert.Equal(3f, window.Sum);
    }

    [Fact]
    public void RollingWindow_IgnoresNonFiniteValues()
    {
        var window = new RollingWindow();
        window.Add(0f, float.NaN);
        window.Add(0f, float.PositiveInfinity);

        Assert.Equal(0, window.Count);
        Assert.Equal(0f, window.Mean);
        Assert.Equal(0f, window.Max);
    }

    [Fact]
    public void RollingWindow_CountsSignChangesOutsideTheEpsilon()
    {
        var window = new RollingWindow();
        float[] values = { 0.5f, -0.5f, 0.001f, -0.4f, 0.3f, 0.3f };
        for (int index = 0; index < values.Length; index++)
            window.Add(index * 0.1f, values[index]);

        Assert.Equal(2, window.SignChanges(0.01f));
    }

    [Fact]
    public void MeasureApproach_SideBySideHullsAreTheirSpacingApart()
    {
        // Two 30 m by 8 m hulls heading north, 20 m apart centre to centre.
        var measured = NavalCopyProbeLogic.MeasureApproach(new Vec2(0f, 0f), new Vec2(0f, 1f), 15f, 4f,
            new Vec2(20f, 0f), new Vec2(0f, 1f), 15f, 4f);

        Assert.Equal(20f, measured.CenterDistance, 3);
        Assert.Equal(12f, measured.Gap, 3);
    }

    [Fact]
    public void MeasureApproach_ABowAtTheBeamIsTheBowDistance()
    {
        // The rammer heads east, its bow 6 m from the target's keel; the target lies north-south.
        var measured = NavalCopyProbeLogic.MeasureApproach(new Vec2(-21f, 0f), new Vec2(1f, 0f), 15f, 4f,
            new Vec2(0f, 0f), new Vec2(0f, 1f), 15f, 4f);

        // 6 m from the keel is 2 m from the target's side.
        Assert.Equal(2f, measured.Gap, 3);
    }

    [Fact]
    public void MeasureApproach_CrossingKeelsOverlap()
    {
        var measured = NavalCopyProbeLogic.MeasureApproach(new Vec2(0f, 0f), new Vec2(1f, 0f), 15f, 4f,
            new Vec2(0f, 0f), new Vec2(0f, 1f), 15f, 4f);

        Assert.Equal(-8f, measured.Gap, 3);
    }

    [Fact]
    public void SegmentDistance_MeasuresEndToEnd()
    {
        Assert.Equal(5f, NavalCopyProbeLogic.SegmentDistance(new Vec2(0f, 0f), new Vec2(10f, 0f), new Vec2(15f, 0f),
            new Vec2(20f, 0f)), 3);
    }

    [Fact]
    public void RecordStep_CountsSignChangesOfThePositionErrorAlongTheHeadingAsOscillations()
    {
        var steering = Observed(NavalCopySteeringTests.Knobs("lead=0"), 0f, Vec2.Zero);

        // Each position is held for a second, well past the smoothing's tau.
        HoldCopyAt(steering, new Vec3(-1f, 0f, 0f));
        HoldCopyAt(steering, new Vec3(1f, 0f, 0f));
        HoldCopyAt(steering, new Vec3(-1f, 0f, 0f));
        // A sideways error has no component along the heading, so it keeps the last sign.
        HoldCopyAt(steering, new Vec3(0f, -1f, 0f));
        HoldCopyAt(steering, new Vec3(-1f, 0f, 0f));

        Assert.Equal(2, steering.Oscillations);
    }

    // Steps the steering for one second with the copy held at a fixed position.
    private static void HoldCopyAt(RecordedSteering steering, Vec3 origin)
    {
        for (int tick = 0; tick < 60; tick++) steering.Compute(Dt, origin, 0f, Vec2.Zero, 0f);
    }

    // R43 S1: the target jumps at each sample arrival by the arrival jitter times the speed; a hull moving smoothly with the
    // owner never crosses its target, so the raw error's flips are no oscillation.
    [Fact]
    public void RecordStep_JitteredArrivalAt10MpsOnASmoothHullCountsNoOscillation()
    {
        var run = RunJitteredArrival(ringingMetres: 0f);

        Assert.True(run.RawFlips > 10, $"{run.RawFlips} raw flips; the model no longer reproduces the R43 artifact");
        Assert.Equal(0, run.Oscillations);
    }

    // A copy really swinging 0.5 m about its target every 2 s under the same jitter still counts its crossings.
    [Fact]
    public void RecordStep_JitteredArrivalAt10MpsStillCountsRealRinging()
    {
        var run = RunJitteredArrival(ringingMetres: 0.5f);

        Assert.InRange(run.Oscillations, 15, 21);
    }

    // An owner at 10 m/s sending every 50 ms on its own frames, arriving 0 to 20 ms late on 60 Hz copy frames, and a copy
    // that follows the owner smoothly at the target's mean offset, plus an optional sine ringing of the given amplitude.
    private static (int RawFlips, long Oscillations) RunJitteredArrival(float ringingMetres)
    {
        const float speed = 10f;
        float meanOffset = MeanTargetOffset(speed);
        var steering = new RecordedSteering(NavalCopySteeringTests.Knobs("lead=0"));
        var sends = JitteredSends(new Random(43));
        int next = 0;
        int rawFlips = 0;
        int lastRawSign = 0;
        for (int tick = 0; tick < 20 * 60; tick++)
        {
            float time = tick * Dt;
            while (next < sends.Count && sends[next].ArrivesAt <= time)
            {
                steering.ObserveOwner(NavalCopySteeringTests.Frame(speed * sends[next].SentAt, 0f, 0f), new Vec2(speed, 0f), 0f);
                next++;
            }

            if (next == 0) continue;
            float ringing = ringingMetres * (float)Math.Sin(Math.PI * time);
            var step = steering.Compute(Dt, new Vec3((speed * time) + meanOffset + ringing, 0f, 0f), 0f, new Vec2(speed, 0f), 0f);
            if (Math.Abs(step.ErrorAlongHeading) < CopyDiagnostics.OscillationDeadbandMetres) continue;

            int sign = Math.Sign(step.ErrorAlongHeading);
            if (lastRawSign != 0 && sign != lastRawSign) rawFlips++;
            lastRawSign = sign;
        }

        return (rawFlips, steering.Oscillations);
    }

    // The along-heading error a copy exactly on the owner sees on average, so the modelled copy sits on the target's mean.
    private static float MeanTargetOffset(float speed)
    {
        var steering = new CopySteering(NavalCopySteeringTests.Knobs("lead=0"));
        var sends = JitteredSends(new Random(43));
        int next = 0;
        float sum = 0f;
        int samples = 0;
        for (int tick = 0; tick < 20 * 60; tick++)
        {
            float time = tick * Dt;
            while (next < sends.Count && sends[next].ArrivesAt <= time)
            {
                steering.ObserveOwner(NavalCopySteeringTests.Frame(speed * sends[next].SentAt, 0f, 0f), new Vec2(speed, 0f), 0f);
                next++;
            }

            if (next == 0) continue;
            sum += steering.Compute(Dt, new Vec3(speed * time, 0f, 0f), 0f, new Vec2(speed, 0f), 0f).ErrorAlongHeading;
            samples++;
        }

        return sum / samples;
    }

    // The owner's sends: every 50 ms on owner frames of 12 to 22 ms, each arriving 0 to 20 ms after it was sent.
    private static System.Collections.Generic.List<(float SentAt, float ArrivesAt)> JitteredSends(Random random)
    {
        var sends = new System.Collections.Generic.List<(float, float)>();
        float ownerTime = 0f;
        float accumulated = 0f;
        float lastArrival = 0f;
        while (ownerTime < 21f)
        {
            float frame = 0.012f + ((float)random.NextDouble() * 0.01f);
            ownerTime += frame;
            accumulated += frame;
            if (accumulated < 0.05f) continue;

            accumulated = Math.Min(accumulated - 0.05f, 0.05f);
            lastArrival = Math.Max(lastArrival, ownerTime + ((float)random.NextDouble() * 0.02f));
            sends.Add((ownerTime, lastArrival));
        }

        return sends;
    }

    [Fact]
    public void RecordStep_ASteadyOffsetIsNoOscillation()
    {
        var steering = Observed(NavalCopySteeringTests.Knobs(), 0f, Vec2.Zero);
        var random = new Random(7);

        // Held 0.3 m behind the target with 0.15 m of noise, so the error never reaches the far side of the deadband.
        for (int tick = 0; tick < 600; tick++)
            steering.Compute(Dt, new Vec3(-0.3f + (((float)random.NextDouble() - 0.5f) * 0.3f), 0f, 0f), 0f, Vec2.Zero, 0f);

        Assert.Equal(0, steering.Oscillations);
    }

    // R42: the old counter flipped on the velocity correction, which thrust noise on a copy sitting on its target alternates.
    [Fact]
    public void RecordStep_ThrustNoiseOnTargetIsNoOscillation()
    {
        var steering = Observed(NavalCopySteeringTests.Knobs(), 0f, new Vec2(5f, 0f));
        var random = new Random(9);
        int correctionFlips = 0;
        int lastCorrectionSign = 0;

        for (int tick = 1; tick <= 600; tick++)
        {
            if (tick % 3 == 0) steering.ObserveOwner(NavalCopySteeringTests.Frame(5f * tick * Dt, 0f, 0f), new Vec2(5f, 0f), 0f);
            float onTarget = steering.Target.Position.x + (5f * Dt);
            float thrust = tick % 2 == 0 ? 0.3f : -0.3f;
            var step = steering.Compute(Dt, new Vec3(onTarget + (((float)random.NextDouble() - 0.5f) * 0.1f), 0f, 0f), 0f,
                new Vec2(5f + thrust, 0f), 0f);

            int correctionSign = Math.Sign(step.DeltaVelocity.x);
            if (correctionSign != 0 && lastCorrectionSign != 0 && correctionSign != lastCorrectionSign) correctionFlips++;
            if (correctionSign != 0) lastCorrectionSign = correctionSign;
        }

        Assert.True(correctionFlips > 100, $"{correctionFlips} correction flips");
        Assert.Equal(0, steering.Oscillations);
    }

    [Fact]
    public void RecordStep_CountsACopyRingingAboutItsTarget()
    {
        var steering = Observed(NavalCopySteeringTests.Knobs(), 0f, Vec2.Zero);

        // 0.5 m either side of the target once a second for 5 s: it crosses at every half second before the end.
        for (int tick = 1; tick <= 300; tick++)
            steering.Compute(Dt, new Vec3(-0.5f * (float)Math.Sin(2 * Math.PI * tick * Dt), 0f, 0f), 0f, Vec2.Zero, 0f);

        Assert.Equal(9, steering.Oscillations);
    }

    [Fact]
    public void RecordSnap_CountsTheSnapAndRestartsTheOscillationSign()
    {
        var steering = Observed(NavalCopySteeringTests.Knobs("lead=0"), 0f, Vec2.Zero);

        HoldCopyAt(steering, new Vec3(-1f, 0f, 0f));
        steering.Diagnostics.RecordSnap();
        // After a snap the copy starts over on its target, so the first sign afterwards is no crossing.
        HoldCopyAt(steering, new Vec3(1f, 0f, 0f));

        Assert.Equal(1, steering.Diagnostics.Snaps);
        Assert.Equal(0, steering.Oscillations);
    }

    // A steering that just took one sample at (x, 0) heading +x, every step of it recorded.
    private static RecordedSteering Observed(CopySteeringKnobs knobs, float x, Vec2 velocity)
    {
        var steering = new RecordedSteering(knobs);
        steering.ObserveOwner(NavalCopySteeringTests.Frame(x, 0f, 0f), velocity, 0f);
        return steering;
    }

    // A steering whose every step is recorded into its diagnostics, as NavalSteeredCopies records it on the fixed tick.
    private sealed class RecordedSteering
    {
        private readonly CopySteeringKnobs knobs;
        private readonly CopySteering steering;

        public RecordedSteering(CopySteeringKnobs knobs)
        {
            this.knobs = knobs;
            steering = new CopySteering(knobs);
        }

        public CopyDiagnostics Diagnostics { get; } = new CopyDiagnostics();

        public long Oscillations => Diagnostics.Oscillations;

        public (Vec2 Position, float Yaw) Target => steering.Target;

        public void ObserveOwner(MatrixFrame frame, Vec2 velocity, float yawRate) => steering.ObserveOwner(frame, velocity, yawRate);

        public CopySteeringStep Compute(float dt, Vec3 origin, float yaw, Vec2 velocity, float yawRate)
        {
            var step = steering.Compute(dt, origin, yaw, velocity, yawRate);
            Diagnostics.RecordStep(0f, dt, knobs.TauSeconds, step);
            return step;
        }
    }
}
#endif
