#if COOP_NAVAL
using Missions.Naval;
using System;
using System.Diagnostics;
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
    private const float Tau = 0.01f;
    private static readonly TimeSpan RunTime = TimeSpan.FromMilliseconds(750);

    // Sample k sits at x = 10k heading +x with velocity (k, 0) and yaw rate k, so any mix of two samples breaks the ratio.
    private static void ObserveSample(CopySteering steering, int k) =>
        steering.ObserveOwner(NavalCopySteeringTests.Frame(10f * k, 0f, 0f), new Vec2(k, 0f), k);

    // Observes, computes and takes snaps on three threads at once; the first mixed sample seen, or null.
    private static string FirstMixedSample(CopySteering steering)
    {
        string mixed = null;
        var clock = Stopwatch.StartNew();
        bool Running() => Volatile.Read(ref mixed) == null && clock.Elapsed < RunTime;
        void Report(string message) => Interlocked.CompareExchange(ref mixed, message, null);

        var observer = Task.Run(() =>
        {
            for (int k = 1; Running(); k = (k % SampleCount) + 1) ObserveSample(steering, k);
        });
        var computer = Task.Run(() =>
        {
            while (Running())
            {
                // Gain 0 and a step of one tau make the velocity change the sample's velocity and the error its extrapolated x.
                var step = steering.Compute(Tau, Vec3.Zero, 0f, Vec2.Zero, 0f);
                if (step.DeltaVelocity.x == 0f && !step.Snap) continue; // a stale owner feeds no velocity forward
                float ratio = step.ErrorAlongHeading / step.DeltaVelocity.x;
                if (step.Snap || ratio < 10f - 1e-3f || ratio > 10f + CopySteering.MaxExtrapolationSeconds + 1e-3f)
                    Report($"step: error {step.ErrorAlongHeading} with velocity {step.DeltaVelocity.x}, snap {step.Snap}");
            }
        });
        var snapper = Task.Run(() =>
        {
            while (Running())
            {
                var snap = steering.TakeSnap();
                if (snap.Velocity.x == 0f && snap.YawRate == 0f) continue; // a stale owner hands over no motion
                float ratio = snap.Frame.origin.x / snap.Velocity.x;
                if (ratio < 10f - 1e-3f || ratio > 10f + CopySteering.MaxExtrapolationSeconds + 1e-3f || snap.YawRate != snap.Velocity.x)
                    Report($"snap: x {snap.Frame.origin.x} with velocity {snap.Velocity.x} and yaw rate {snap.YawRate}");
            }
        });

        Task.WaitAll(observer, computer, snapper);
        return mixed;
    }

    [Fact]
    public void ComputeAndTakeSnap_WhileSamplesArrive_ReadOneWholeSample()
    {
        var steering = new CopySteering(NavalCopySteeringTests.Knobs("gain=0", "tau=0.01", "lead=0", "snap=500"));
        ObserveSample(steering, 1);

        Assert.Null(FirstMixedSample(steering));
    }
}
#endif
