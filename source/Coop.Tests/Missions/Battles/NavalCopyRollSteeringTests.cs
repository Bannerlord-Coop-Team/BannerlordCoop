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

    // A level frame at (x, 0) heading +x, rolled right-handed about its forward axis (Rodrigues' rotation).
    internal static MatrixFrame Rolled(float roll, float x = 0f)
    {
        var level = NavalCopySteeringTests.Frame(x, 0f, 0f);
        var forward = level.rotation.f;
        float sin = (float)Math.Sin(roll);
        float cos = (float)Math.Cos(roll);
        Vec3 Turn(Vec3 axis) => (axis * cos) + (Vec3.CrossProduct(forward, axis) * sin) + (forward * (Vec3.DotProduct(forward, axis) * (1f - cos)));
        return new MatrixFrame(new Mat3(Turn(level.rotation.s), forward, Turn(level.rotation.u)), level.origin);
    }

    // A steering that just took one owner sample rolled by the given angle and turning at the given roll rate.
    private static CopySteering ObservedRolled(CopySteeringKnobs knobs, float roll, float rollRate = 0f)
    {
        var steering = new CopySteering(knobs);
        steering.ObserveOwner(Rolled(roll), Vec2.Zero, 0f, rollRate);
        return steering;
    }

    // The roll coordinate turns with the angular velocity's forward component, opposite to the probe's logged tilt.
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

    // A free copy (no roll passed in) keeps its buoyancy's roll untouched.
    [Fact]
    public void Compute_WithoutARoll_LeavesTheRollToBuoyancy()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("tau=0.01"), 5f * DegToRad, 0.1f);

        var step = steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f);

        Assert.Equal(0f, step.DeltaRollRate);
    }

    // The roll knob at 0 is the off switch, also for the owner's roll rate.
    [Fact]
    public void Compute_RollGainZero_TurnsTheRollSteeringOff()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("tau=0.01", "roll=0"), 5f * DegToRad, 0.1f);

        var step = steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f, (0f, 0f));

        Assert.Equal(0f, step.DeltaRollRate);
    }

    // The step is the owner's roll rate plus the gain times the error, minus the copy's rate, blended by dt / tau.
    [Fact]
    public void Compute_PullsTheRollTowardTheOwnersAtItsRollRateAndBlendsWithTau()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("roll=5", "tau=0.2", "lead=0"), 1f * DegToRad, 0.03f);

        var step = steering.Compute(0.05f, Vec3.Zero, 0f, Vec2.Zero, 0f, (0f, 0.02f));

        float error = (1f * DegToRad) + (0.03f * 0.05f);
        Assert.Equal((0.03f + (5f * error) - 0.02f) * (0.05f / 0.2f), step.DeltaRollRate, 5);
    }

    // A large roll error is closed at the clamp rate, not in one jerk.
    [Fact]
    public void Compute_ClampsTheRollCorrection()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("roll=20", "tau=0.01", "lead=0"), 30f * DegToRad);

        var step = steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f, (0f, 0f));

        Assert.Equal(CopySteering.RollClampDegreesPerSecond * DegToRad, step.DeltaRollRate, 4);
    }

    // The target is the sample's roll moved on by the owner's own roll rate for the sample's age, capped like the yaw.
    [Fact]
    public void TargetRoll_ExtrapolatesTheOwnersRollRateForTheSampleAge()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("lead=0"), 0.1f, 0.2f);

        steering.Compute(0.1f, Vec3.Zero, 0f, Vec2.Zero, 0f);
        float early = steering.TargetRoll;
        steering.Compute(1f, Vec3.Zero, 0f, Vec2.Zero, 0f);

        Assert.Equal(0.1f + (0.2f * 0.1f), early, 5);
        Assert.Equal(0.1f + (0.2f * CopySteering.MaxExtrapolationSeconds), steering.TargetRoll, 5);
    }

    // R53 review P2: a teleporting owner's new roll is a pose, not a rotation, so the first step after the snap adds no roll kick.
    [Fact]
    public void Compute_RightAfterAnOwnerTeleportSnap_AddsNoRollKick()
    {
        var steering = ObservedRolled(NavalCopySteeringTests.Knobs("lead=0"), 0f);
        for (int tick = 0; tick < 3; tick++) steering.Compute(Dt, Vec3.Zero, 0f, Vec2.Zero, 0f, (0f, 0f));

        steering.ObserveOwner(Rolled(30f * DegToRad, x: 30f), Vec2.Zero, 0f, 0f);
        Assert.True(steering.IsSnapQueued);
        var snapped = steering.TakeSnapFrame();
        var step = steering.Compute(Dt, snapped.origin, 0f, Vec2.Zero, 0f, (CopySteering.RollOf(snapped.rotation), 0f));

        Assert.Equal(30f * DegToRad, steering.TargetRoll, 5);
        Assert.Equal(0f, step.DeltaRollRate, 5);
    }

    // R51 item 25: the owner heels and rolls under the rope's rail forces that its copy never feels, on top of the waves both
    // float on. Paired as the item pairs them, with margin; only the steered copy passes, under random and alternating delay.
    [Theory]
    [InlineData(Delivery.RandomJitter)]
    [InlineData(Delivery.AlternatingDelay)]
    public void Compute_ARopedCopyFollowsItsHeeledOwnersRollWithinTheItem25Limits(Delivery delivery)
    {
        var steered = RopedRoll.Run(steerRoll: true, delivery);
        var unsteered = RopedRoll.Run(steerRoll: false, delivery);

        Assert.True(Math.Abs(steered.MeanDegrees) <= 0.35f && steered.SdDegrees <= 0.7f && Math.Abs(steered.Gain - 1f) <= 0.07f,
            $"steered: {steered}");
        Assert.True(Math.Abs(unsteered.MeanDegrees) > 0.5f, $"unsteered: {unsteered}");
    }

    /// <summary>How the modelled owner's 20 Hz samples reach the copy.</summary>
    public enum Delivery
    {
        /// <summary>80 ms late, +- 20 ms at random.</summary>
        RandomJitter,
        /// <summary>Alternately 60 and 100 ms late, so consecutive arrivals bunch and stretch (R53 review P1).</summary>
        AlternatingDelay,
    }

    // A roped pair modelled as the R51 investigation decomposed it: wave roll on both hulls plus the owner's rope roll,
    // about 3.4 degrees of heel and a 0.17-0.3 Hz sway.
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

        // The paired statistics, for an assertion message.
        public override string ToString() => FormattableString.Invariant($"mean {MeanDegrees} deg, sd {SdDegrees} deg, gain {Gain}");

        // The wave roll both hulls float on, in radians.
        private static float Wave(float time) => Swell(4f, 0.13f, time, 0f) + Swell(1.5f, 0.31f, time, 1f);

        // The owner's roll in radians: the waves, plus the heel and sway its rope's rail forces add.
        private static float Owner(float time) => Wave(time) - (3.4f * DegToRad) + Swell(2.5f, 0.17f, time, 0.5f) + Swell(1.5f, 0.3f, time, 2f);

        // The owner's roll rate in rad/s, which its samples carry.
        private static float OwnerRate(float time) => SwellRate(4f, 0.13f, time, 0f) + SwellRate(1.5f, 0.31f, time, 1f)
            + SwellRate(2.5f, 0.17f, time, 0.5f) + SwellRate(1.5f, 0.3f, time, 2f);

        // One sinusoidal roll component in radians.
        private static float Swell(float degrees, float hertz, float time, float phase) =>
            degrees * DegToRad * (float)Math.Sin((2 * Math.PI * hertz * time) + phase);

        // The rate of one sinusoidal roll component in rad/s.
        private static float SwellRate(float degrees, float hertz, float time, float phase) =>
            degrees * DegToRad * (float)(2 * Math.PI * hertz * Math.Cos((2 * Math.PI * hertz * time) + phase));

        // When sample n arrives: random jitter keeps arrival order; the alternating delay bunches and stretches them.
        private static float ArrivalOf(int sample, Delivery delivery, Random random)
        {
            float sentAt = sample * SendInterval;
            if (delivery == Delivery.AlternatingDelay) return sentAt + (sample % 2 == 0 ? 0.06f : 0.1f);

            return sentAt + Latency + (((float)random.NextDouble() - 0.5f) * 0.04f);
        }

        // Steps the copy for 70 s and pairs its roll with the owner's over the last 60 s, at every physics step.
        public static RopedRoll Run(bool steerRoll, Delivery delivery)
        {
            var steering = new CopySteering(new CopySteeringKnobs());
            var random = new Random(51);
            float roll = Wave(0f);
            float rate = 0f;
            int nextSample = 0;
            float nextArrival = ArrivalOf(0, delivery, random);
            var copy = new List<float>();
            var owner = new List<float>();

            for (int tick = 0; tick < 70 * 60; tick++)
            {
                float time = tick * Dt;
                while (nextArrival <= time)
                {
                    float sentAt = nextSample * SendInterval;
                    steering.ObserveOwner(Rolled(Owner(sentAt)), Vec2.Zero, 0f, OwnerRate(sentAt));
                    nextSample++;
                    nextArrival = Math.Max(nextArrival, ArrivalOf(nextSample, delivery, random));
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
