#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using NavalDLC.Missions.Objects;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Missions.Naval;

/// <summary>DEBUG: (time, value) samples over the last <see cref="Seconds"/> seconds of this client's mission clock.</summary>
internal sealed class RollingWindow
{
    internal const float DefaultSeconds = 10f;

    private readonly List<(float Time, float Value)> samples = new List<(float, float)>();

    public RollingWindow(float seconds = DefaultSeconds)
    {
        Seconds = seconds;
    }

    public float Seconds { get; }

    public int Count => samples.Count;

    public void Add(float time, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;

        samples.Add((time, value));
        int stale = 0;
        while (stale < samples.Count && time - samples[stale].Time > Seconds) stale++;
        if (stale > 0) samples.RemoveRange(0, stale);
    }

    public float Mean => samples.Count == 0 ? 0f : samples.Average(sample => sample.Value);

    public float Max => samples.Count == 0 ? 0f : samples.Max(sample => sample.Value);

    public float Sum => samples.Sum(sample => sample.Value);

    /// <summary>The nearest-rank percentile (0 to 100) of the window's values, so one GC pause doesn't stand for the window.</summary>
    public float Percentile(float percent)
    {
        if (samples.Count == 0) return 0f;

        var sorted = samples.Select(sample => sample.Value).OrderBy(value => value).ToList();
        int rank = (int)Math.Ceiling(percent * sorted.Count / 100.0);
        return sorted[Math.Max(0, Math.Min(sorted.Count - 1, rank - 1))];
    }

    /// <summary>How often the sign flips between samples; samples within <paramref name="epsilon"/> of zero keep the last sign.</summary>
    public int SignChanges(float epsilon)
    {
        int changes = 0;
        int last = 0;
        foreach (var sample in samples)
        {
            if (Math.Abs(sample.Value) < epsilon) continue;

            int sign = sample.Value > 0f ? 1 : -1;
            if (last != 0 && sign != last) changes++;
            last = sign;
        }

        return changes;
    }

    public void Clear() => samples.Clear();
}

/// <summary>
/// DEBUG: how far one copy sits from its owner's estimated pose, how hard its steering pushes and how often it crosses its
/// target over the last 10 s, plus its snap and oscillation totals.
/// </summary>
internal sealed class CopyDiagnostics
{
    // A position error along the heading smaller than this (m) has no sign, so a copy sitting on its target does not oscillate.
    internal const float OscillationDeadbandMetres = 0.1f;

    private float smoothedAlong;
    private bool hasSmoothedAlong;
    private int lastSign;

    /// <summary>Planar distance to the owner's estimated position in metres.</summary>
    public RollingWindow Distance { get; } = new RollingWindow();

    /// <summary>Heading difference to the owner's estimated yaw in degrees.</summary>
    public RollingWindow HeadingDegrees { get; } = new RollingWindow();

    /// <summary>Height difference to the owner's sample, which the local water decides.</summary>
    public RollingWindow Heave { get; } = new RollingWindow();

    /// <summary>Size of each steering step's velocity change in m/s.</summary>
    public RollingWindow Correction { get; } = new RollingWindow();

    /// <summary>The along-heading position error to the target, low-passed over tau; its sign changes over the window count as oscillations.</summary>
    public RollingWindow ErrorAlongHeading { get; } = new RollingWindow();

    /// <summary>How far the target moved at each sample arrival; owner frame-time jitter times speed shows here.</summary>
    public RollingWindow TargetJump { get; } = new RollingWindow();

    /// <summary>Sign changes of the smoothed along-heading error since the copy was tracked, restarted by nothing.</summary>
    public long Oscillations { get; private set; }

    /// <summary>Teleports of the copy to its owner's frame.</summary>
    public long Snaps { get; private set; }

    /// <summary>
    /// Records one steering step's velocity change and its along-heading error, low-passed over <paramref name="tauSeconds"/>
    /// because the raw error flips at each sample arrival as the target jumps by the owner's frame-time jitter times its speed (R43).
    /// </summary>
    public void RecordStep(float time, float dt, float tauSeconds, CopySteeringStep step)
    {
        Correction.Add(time, step.DeltaVelocity.Length);
        if (step.Snap)
        {
            ErrorAlongHeading.Add(time, 0f);
            return;
        }

        float smoothed = SmoothAlong(step.ErrorAlongHeading, dt, tauSeconds);
        ErrorAlongHeading.Add(time, smoothed);
        CountOscillation(smoothed);
    }

    /// <summary>Counts a snap and restarts the smoothing and the last sign, since the copy now sits on its target.</summary>
    public void RecordSnap()
    {
        Snaps++;
        lastSign = 0;
        hasSmoothedAlong = false;
    }

    /// <summary>The oscillations within the window, by the same deadband as the total.</summary>
    public int WindowOscillations => ErrorAlongHeading.SignChanges(OscillationDeadbandMetres);

    // The along-heading error low-passed with the steering's own tau, starting from the first error after a reset.
    private float SmoothAlong(float along, float dt, float tauSeconds)
    {
        smoothedAlong = hasSmoothedAlong ? smoothedAlong + ((along - smoothedAlong) * Math.Min(1f, dt / tauSeconds)) : along;
        hasSmoothedAlong = true;
        return smoothedAlong;
    }

    // A sign change of the smoothed along-heading error, the copy crossing its target; errors in the deadband keep the last sign.
    private void CountOscillation(float along)
    {
        if (Math.Abs(along) < OscillationDeadbandMetres) return;

        int sign = along > 0f ? 1 : -1;
        if (lastSign != 0 && sign != lastSign) Oscillations++;
        lastSign = sign;
    }

    /// <summary>The drift, heading, heave and correction windows with the counters and the steering's sample age.</summary>
    public object Inspect(CopySteering steering) => new
    {
        drift = new { samples = Distance.Count, meanM = Round(Distance.Mean), maxM = Round(Distance.Max) },
        heading = new { meanDeg = Round(HeadingDegrees.Mean), maxDeg = Round(HeadingDegrees.Max) },
        heave = new { meanM = Round(Heave.Mean), maxM = Round(Heave.Max) },
        targetJump = new { samples = TargetJump.Count, meanM = Round(TargetJump.Mean), maxM = Round(TargetJump.Max) },
        correction = new
        {
            samples = Correction.Count,
            meanMps = Round(Correction.Mean),
            maxMps = Round(Correction.Max),
            oscillations10s = WindowOscillations,
            oscillationsTotal = Oscillations,
            snaps = Snaps,
            sampleAgeS = steering == null ? (float?)null : Round(steering.SecondsSinceSample),
        },
    };

    /// <summary>The value rounded to 3 decimals for the inspect output.</summary>
    internal static float Round(float value) => (float)Math.Round(value, 3);
}
#endif
