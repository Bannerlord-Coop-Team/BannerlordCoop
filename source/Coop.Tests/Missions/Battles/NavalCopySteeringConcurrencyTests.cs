#if COOP_NAVAL
using Missions.Naval;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// <see cref="CopySteering"/> observed on the mission tick while the fixed tick computes (#4191 review): every step and snap
/// reads one whole owner sample.
/// </summary>
public class NavalCopySteeringConcurrencyTests
{
    private const int SampleCount = 40;
    private const int IterationsPerWorker = 200_000;
    private const float Tau = 0.01f;
    // Only a failure safeguard; each worker stops after its own iteration count.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Sample k sits at x = 10k heading +x with velocity (k, 0) and yaw rate k, so any mix of two samples breaks the ratio.
    private static void ObserveSample(CopySteering steering, int k) =>
        steering.ObserveOwner(NavalCopySteeringTests.Frame(10f * k, 0f, 0f), new Vec2(k, 0f), k);

    // What the three workers saw: the first mixed sample, if any, and how many fresh steps and snaps were checked.
    private sealed class RunResult
    {
        public string Mixed;
        public int CheckedSteps;
        public int CheckedSnaps;
    }

    // Starts observing, computing and taking snaps on three threads together, each for a fixed number of iterations.
    private static RunResult RunConcurrently(CopySteering steering)
    {
        var result = new RunResult();
        using var start = new Barrier(3);
        bool Running() => Volatile.Read(ref result.Mixed) == null;
        void Report(string message) => Interlocked.CompareExchange(ref result.Mixed, message, null);

        var observer = Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            for (int i = 0, k = 1; i < IterationsPerWorker && Running(); i++, k = (k % SampleCount) + 1) ObserveSample(steering, k);
        }, TaskCreationOptions.LongRunning);
        var computer = Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            for (int i = 0; i < IterationsPerWorker && Running(); i++)
            {
                // Gain 0 and a step of one tau make the velocity change the sample's velocity and the error its extrapolated x.
                var step = steering.Compute(Tau, Vec3.Zero, 0f, Vec2.Zero, 0f);
                if (step.DeltaVelocity.x == 0f && !step.Snap) continue; // a stale owner feeds no velocity forward
                result.CheckedSteps++;
                float ratio = step.ErrorAlongHeading / step.DeltaVelocity.x;
                if (step.Snap || ratio < 10f - 1e-3f || ratio > 10f + CopySteering.MaxExtrapolationSeconds + 1e-3f)
                    Report($"step: error {step.ErrorAlongHeading} with velocity {step.DeltaVelocity.x}, snap {step.Snap}");
            }
        }, TaskCreationOptions.LongRunning);
        var snapper = Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            for (int i = 0; i < IterationsPerWorker && Running(); i++)
            {
                var snap = steering.TakeSnap();
                if (snap.Velocity.x == 0f && snap.YawRate == 0f) continue; // a stale owner hands over no motion
                result.CheckedSnaps++;
                float ratio = snap.Frame.origin.x / snap.Velocity.x;
                if (ratio < 10f - 1e-3f || ratio > 10f + CopySteering.MaxExtrapolationSeconds + 1e-3f || snap.YawRate != snap.Velocity.x)
                    Report($"snap: x {snap.Frame.origin.x} with velocity {snap.Velocity.x} and yaw rate {snap.YawRate}");
            }
        }, TaskCreationOptions.LongRunning);

        Assert.True(Task.WaitAll(new[] { observer, computer, snapper }, Timeout), "steering workers did not finish");
        return result;
    }

    [Fact]
    public void ComputeAndTakeSnap_WhileSamplesArrive_ReadOneWholeSample()
    {
        var steering = new CopySteering(NavalCopySteeringTests.Knobs("gain=0", "tau=0.01", "lead=0", "snap=500"));
        ObserveSample(steering, 1);

        var result = RunConcurrently(steering);

        Assert.Null(result.Mixed);
        Assert.True(result.CheckedSteps > 0, "no fresh step was checked");
        Assert.True(result.CheckedSnaps > 0, "no fresh snap was checked");
    }
}
#endif
