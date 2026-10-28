#if COOP_NAVAL
using Missions.Naval;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>The roll half of <see cref="CopySteering"/>: a roped copy's roll steered toward its owner's (R51 item 25, D3 amendment).</summary>
public class NavalCopyRollSteeringTests
{
    private const float Dt = 1f / 60f;
    private const float SendInterval = 0.05f;
    private const float DegToRad = CopySteering.DegToRad;

    // A level frame at the origin heading +x, rolled right-handed about its forward axis (Rodrigues' rotation).
    internal static MatrixFrame Rolled(float roll)
    {
        var level = NavalCopySteeringTests.Frame(0f, 0f, 0f);
        var forward = level.rotation.f;
        float sin = (float)Math.Sin(roll);
        float cos = (float)Math.Cos(roll);
        Vec3 Turn(Vec3 axis) => (axis * cos) + (Vec3.CrossProduct(forward, axis) * sin) + (forward * (Vec3.DotProduct(forward, axis) * (1f - cos)));
        return new MatrixFrame(new Mat3(Turn(level.rotation.s), forward, Turn(level.rotation.u)), level.origin);
    }

    // A steering that just took one owner sample rolled by the given angle.
    private static CopySteering ObservedRolled(CopySteeringKnobs knobs, float roll)
    {
        var steering = new CopySteering(knobs);
        steering.ObserveOwner(Rolled(roll), Vec2.Zero, 0f);
        return steering;
    }

    [Theory]
    [InlineData(0.05f)]
    [InlineData(-0.3f)]
    public void RollOf_ReadsARightHandedTurnAboutForward_OppositeToTheProbesTilt(float roll)
    {
        var rotation = Rolled(roll).rotation;

        Assert.Equal(roll, CopySteering.RollOf(rotation), 4);
        // NavalRollProbe logs the side axis's tilt, asin(s.z).
        Assert.Equal(-roll, (float)Math.Asin(rotation.s.z), 4);
    }

    [Fact]
    public void Compute_WithoutARoll_LeavesTheRollToBuoyancy()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("tau=0.01"), 5f * DegToRad);

        var step = steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f);

        Assert.Equal(0f, step.DeltaRollRate);
    }

    [Fact]
    public void Compute_RollGainZero_TurnsTheRollSteeringOff()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("tau=0.01", "roll=0"), 5f * DegToRad);

        var step = steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f, (0f, 0.1f));

        Assert.Equal(0f, step.DeltaRollRate);
    }

    [Fact]
    public void Compute_PullsTheRollTowardTheOwnersInProportionAndBlendsWithTau()
    {
        // 1 degree short of the owner's roll at gain 5/s, against the copy's own 0.02 rad/s, blended by dt / tau.
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("roll=5", "tau=0.2", "lead=0"), 1f * DegToRad);

        var step = steering.Compute(0.05f, Vec3.Zero, 0f, Vec2.Zero, 0f, (0f, 0.02f));

        Assert.Equal(((5f * DegToRad) - 0.02f) * (0.05f / 0.2f), step.DeltaRollRate, 5);
    }

    [Fact]
    public void Compute_ClampsTheRollCorrection()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("roll=20", "tau=0.01", "lead=0"), 30f * DegToRad);

        var step = steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f, (0f, 0f));

        Assert.Equal(CopySteering.RollClampDegreesPerSecond * DegToRad, step.DeltaRollRate, 4);
    }

    [Fact]
    public void ObserveOwner_DerivesTheRollRateFromConsecutiveSamplesAndExtrapolatesTheTarget()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("lead=0"), 0f);
        for (int tick = 0; tick < 3; tick++) steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f);

        steering.ObserveOwner(Rolled(0.01f), Vec2.Zero, 0f);
        steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f);

        // 0.01 rad over the 3 physics steps between the samples, then moved on by the 1 step since.
        Assert.Equal(0.01f + (0.01f / (3 * Dt) * Dt), steering.TargetRoll, 5);
    }

    [Fact]
    public void ObserveOwner_TwoSamplesWithNoStepBetween_KeepTheLastRollRate()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("lead=0"), 0f);
        for (int tick = 0; tick < 3; tick++) steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f);
        steering.ObserveOwner(Rolled(0.01f), Vec2.Zero, 0f);

        steering.ObserveOwner(Rolled(0.02f), Vec2.Zero, 0f);
        steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f);

        Assert.Equal(0.02f + (0.01f / (3 * Dt) * Dt), steering.TargetRoll, 5);
    }

    [Fact]
    public void ObserveOwner_TheFirstSampleAfterAStaleGapHasNoRollRate()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("lead=0"), 0f);
        steering.Compute(CopySteering.StaleSeconds + 0.1f, Vec3.Zero, 0f, Vec2.Zero, 0f);

        steering.ObserveOwner(Rolled(0.2f), Vec2.Zero, 0f);
        steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f);

        Assert.Equal(0.2f, steering.TargetRoll, 5);
    }

    // R51 item 25: the owner heels and rolls under the rope's rail forces that its copy never feels, on top of the waves both
    // float on. Paired as the item pairs them; only the steered copy passes.
    [Fact]
    public void Compute_ARopedCopyFollowsItsHeeledOwnersRollWithinTheItem25Limits()
    {
        var steered = RopedRoll.Run(steerRoll: true);
        var unsteered = RopedRoll.Run(steerRoll: false);

        Assert.True(Math.Abs(steered.MeanDegrees) <= 0.5f && steered.SdDegrees <= 1f && steered.Gain >= 0.9f && steered.Gain <= 1.1f,
            $"steered: {steered}");
        Assert.True(Math.Abs(unsteered.MeanDegrees) > 0.5f, $"unsteered: {unsteered}");
    }

    // A roped pair modelled as the R51 investigation decomposed it: wave roll on both hulls plus the owner's rope roll,
    // about 3.4 degrees of heel and a 0.17-0.3 Hz sway, with samples at 20 Hz arriving 80 +- 20 ms late.
    private sealed class RopedRoll
    {
        // The copy's local buoyancy as a roll spring toward the wave roll, a natural period of about 4.4 s, lightly damped.
        // Unsteered, this model gives about R51's numbers: mean 3.4 deg, sd 3.2 deg.
        private const float BuoyancyStiffness = 2f;
        private const float BuoyancyDamping = 0.4f;
        private const float Latency = 0.08f;

        public float MeanDegrees { get; private set; }
        public float SdDegrees { get; private set; }
        public float Gain { get; private set; }

        public override string ToString() => FormattableString.Invariant($"mean {MeanDegrees} deg, sd {SdDegrees} deg, gain {Gain}");

        private static float Wave(float time) => (4f * DegToRad * Sin(0.13f, time, 0f)) + (1.5f * DegToRad * Sin(0.31f, time, 1f));

        private static float Owner(float time) => Wave(time) - (3.4f * DegToRad) + (2.5f * DegToRad * Sin(0.17f, time, 0.5f))
            + (1.5f * DegToRad * Sin(0.3f, time, 2f));

        private static float Sin(float hertz, float time, float phase) => (float)Math.Sin((2 * Math.PI * hertz * time) + phase);

        // Steps the copy for 70 s and pairs its roll with the owner's over the last 60 s, at every physics step.
        public static RopedRoll Run(bool steerRoll)
        {
            var steering = new CopySteering(new CopySteeringKnobs());
            var random = new Random(51);
            float roll = Wave(0f);
            float rate = 0f;
            int nextSample = 0;
            float nextArrival = Latency;
            var copy = new List<float>();
            var owner = new List<float>();

            for (int tick = 0; tick < 70 * 60; tick++)
            {
                float time = tick * Dt;
                while (nextArrival <= time)
                {
                    steering.ObserveOwner(Rolled(Owner(nextSample * SendInterval)), Vec2.Zero, 0f);
                    nextSample++;
                    float jitter = ((float)random.NextDouble() - 0.5f) * 0.04f;
                    nextArrival = Math.Max(nextArrival, (nextSample * SendInterval) + Latency + jitter);
                }

                rate += ((-BuoyancyStiffness * (roll - Wave(time))) - (BuoyancyDamping * rate)) * Dt;
                var step = steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f, steerRoll ? (roll, rate) : ((float, float)?)null);
                rate += step.DeltaRollRate;
                roll += rate * Dt;
                if (time < 10f) continue;

                copy.Add(roll / DegToRad);
                owner.Add(Owner(time + Dt) / DegToRad);
            }

            return Paired(copy.ToArray(), owner.ToArray());
        }

        // Item 25's statistics: mean and population sd of copy minus owner, and the gain as the slope of the de-meaned copy
        // roll on the de-meaned owner roll.
        private static RopedRoll Paired(float[] copy, float[] owner)
        {
            float copyMean = copy.Average();
            float ownerMean = owner.Average();
            var offsets = copy.Zip(owner, (c, o) => c - o).ToArray();
            float mean = offsets.Average();
            float covariance = copy.Zip(owner, (c, o) => (c - copyMean) * (o - ownerMean)).Sum();
            float ownerVariance = owner.Sum(o => (o - ownerMean) * (o - ownerMean));
            return new RopedRoll
            {
                MeanDegrees = mean,
                SdDegrees = (float)Math.Sqrt(offsets.Average(d => (d - mean) * (d - mean))),
                Gain = covariance / ownerVariance,
            };
        }
    }
}
#endif
