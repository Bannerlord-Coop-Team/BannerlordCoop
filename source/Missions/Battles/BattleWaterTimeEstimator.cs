using System;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Battles;

/// <summary>
/// Turns the battle host's water time samples into this client's water time: one adoption jump at most, then forward-only
/// snaps and slewing, so after adoption the written time never runs backwards.
/// </summary>
public interface IBattleWaterTimeEstimator
{
    /// <summary>Whether a host sample has set the offset target.</summary>
    bool HasHostTime { get; }

    /// <summary>The smoothed offset the applied one is slewing to.</summary>
    double TargetOffset { get; }

    /// <summary>The offset added to the local clock this frame.</summary>
    double AppliedOffset { get; }

    /// <summary>Starts the battle clock and returns its first time: the host's if one arrived already, else <paramref name="startTime"/>.</summary>
    float Begin(float localTime, float startTime);

    /// <summary>Feeds one host sample, given the local water clock when it was applied.</summary>
    void AddHostSample(float hostTime, float localTime);

    /// <summary>Advances to <paramref name="localTime"/> and returns the water time to write; a host holds its own offset.</summary>
    float Advance(float localTime, bool isHost);
}

/// <inheritdoc cref="IBattleWaterTimeEstimator"/>
public class BattleWaterTimeEstimator : IBattleWaterTimeEstimator
{
    // Weight of a new windowed offset in the smoothed target; one sample a second settles in a few seconds.
    internal const double SmoothingFactor = 0.3;
    // The applied offset moves at most this fraction of the local advance: waves run at 75% to 125% speed, never backwards.
    internal const double SlewFraction = 0.25;
    // A forward gap beyond this (20 water units = 2 s of mission time) on every sample of the window is snapped, not slewed.
    internal const double SnapThreshold = 20.0;
    // Transit delay only ever makes an offset look smaller, so the window's largest offset is the best estimate.
    internal const int SampleWindow = 3;

    private readonly Queue<double> window = new Queue<double>();
    private double? lastLocal;
    private bool begun;
    private bool adoptionPending;
    private bool forwardSnapPending;

    public bool HasHostTime { get; private set; }

    public double TargetOffset { get; private set; }

    public double AppliedOffset { get; private set; }

    public float Begin(float localTime, float startTime)
    {
        begun = true;
        lastLocal = localTime;
        // A host time that arrived during loading is adopted here, before any frame shows the battle's water.
        if (HasHostTime) AppliedOffset = TargetOffset;
        else AppliedOffset = TargetOffset = (double)startTime - localTime;
        adoptionPending = false;
        return (float)(localTime + AppliedOffset);
    }

    public void AddHostSample(float hostTime, float localTime)
    {
        double offset = (double)hostTime - localTime;
        window.Enqueue(offset);
        if (window.Count > SampleWindow) window.Dequeue();

        if (!HasHostTime)
        {
            HasHostTime = true;
            TargetOffset = offset;
            // The one adoption jump, in either direction; a small disagreement is slewed instead.
            adoptionPending = begun && Math.Abs(offset - AppliedOffset) > SnapThreshold;
            return;
        }

        TargetOffset += SmoothingFactor * (window.Max() - TargetOffset);
        if (window.Count == SampleWindow && window.Min() - AppliedOffset > SnapThreshold)
        {
            TargetOffset = window.Max();
            forwardSnapPending = true;
        }
    }

    public float Advance(float localTime, bool isHost)
    {
        double step = lastLocal.HasValue ? Math.Max(0.0, localTime - lastLocal.Value) : 0.0;
        lastLocal = localTime;
        if (isHost) return Hold(localTime);

        if (adoptionPending || (forwardSnapPending && TargetOffset > AppliedOffset))
        {
            AppliedOffset = TargetOffset;
        }
        else
        {
            double maxStep = SlewFraction * step;
            AppliedOffset += Math.Max(-maxStep, Math.Min(maxStep, TargetOffset - AppliedOffset));
        }
        adoptionPending = false;
        forwardSnapPending = false;
        return (float)(localTime + AppliedOffset);
    }

    // A host is the time source: it keeps the offset it has, so a promoted host continues from its own current water time.
    private float Hold(float localTime)
    {
        TargetOffset = AppliedOffset;
        adoptionPending = false;
        forwardSnapPending = false;
        window.Clear();
        return (float)(localTime + AppliedOffset);
    }
}
