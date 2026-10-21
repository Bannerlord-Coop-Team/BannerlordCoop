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

/// <summary>DEBUG: how far one copy sits from its owner's estimated pose, and how hard its steering pushes, over the last 10 s.</summary>
internal sealed class CopyDiagnostics
{
    public RollingWindow Distance { get; } = new RollingWindow();

    public RollingWindow HeadingDegrees { get; } = new RollingWindow();

    /// <summary>Height difference to the owner's sample, which the local water decides.</summary>
    public RollingWindow Heave { get; } = new RollingWindow();

    public RollingWindow Correction { get; } = new RollingWindow();

    /// <summary>The position error to the target along the heading, whose sign changes over the window count as oscillations.</summary>
    public RollingWindow ErrorAlongHeading { get; } = new RollingWindow();

    /// <summary>Records one steering step's velocity change and along-heading error.</summary>
    public void RecordStep(float time, CopySteeringStep step)
    {
        Correction.Add(time, step.DeltaVelocity.Length);
        ErrorAlongHeading.Add(time, step.ErrorAlongHeading);
    }

    /// <summary>The drift, heading, heave and correction windows with the steering's counters.</summary>
    public object Inspect(CopySteering steering) => new
    {
        drift = new { samples = Distance.Count, meanM = Round(Distance.Mean), maxM = Round(Distance.Max) },
        heading = new { meanDeg = Round(HeadingDegrees.Mean), maxDeg = Round(HeadingDegrees.Max) },
        heave = new { meanM = Round(Heave.Mean), maxM = Round(Heave.Max) },
        correction = new
        {
            samples = Correction.Count,
            meanMps = Round(Correction.Mean),
            maxMps = Round(Correction.Max),
            oscillations10s = ErrorAlongHeading.SignChanges(CopySteering.OscillationDeadbandMetres),
            oscillationsTotal = steering?.Oscillations,
            snaps = steering?.Snaps,
            sampleAgeS = steering == null ? (float?)null : Round(steering.SecondsSinceSample),
        },
    };

    internal static float Round(float value) => (float)Math.Round(value, 3);
}
#endif
