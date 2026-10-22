using System;

namespace Missions.Battles;

/// <summary>
/// Turns the battle host's water time samples into this client's water time: the smoothed offset from the local clock,
/// adopted at once on the first sample and slewed afterwards so waves neither pop nor run backwards.
/// </summary>
public interface IBattleWaterTimeEstimator
{
    /// <summary>Whether a host sample has set the offset target.</summary>
    bool HasHostTime { get; }

    /// <summary>The smoothed offset the applied one is slewing to.</summary>
    double TargetOffset { get; }

    /// <summary>The offset added to the local clock this frame.</summary>
    double AppliedOffset { get; }

    /// <summary>Feeds one host sample, given the local water clock when it was applied.</summary>
    void AddHostSample(float hostTime, float localTime);

    /// <summary>Advances to <paramref name="localTime"/> and returns the water time to write; a host holds its own offset.</summary>
    float Advance(float localTime, bool isHost);
}

/// <inheritdoc cref="IBattleWaterTimeEstimator"/>
public class BattleWaterTimeEstimator : IBattleWaterTimeEstimator
{
    // Weight of a new sample in the smoothed target; one sample a second settles in a few seconds.
    internal const double SmoothingFactor = 0.3;
    // The applied offset moves at most this fraction of the local advance, so waves play 25% fast or slow, never backwards.
    internal const double SlewFraction = 0.25;
    // A disagreement beyond this is adopted at once (20 water units = 2 s of mission time), since slewing it would take minutes.
    internal const double SnapThreshold = 20.0;

    private double? lastLocal;
    private bool snapPending;

    public bool HasHostTime { get; private set; }

    public double TargetOffset { get; private set; }

    public double AppliedOffset { get; private set; }

    public void AddHostSample(float hostTime, float localTime)
    {
        double sample = (double)hostTime - localTime;
        if (!HasHostTime || Math.Abs(sample - TargetOffset) > SnapThreshold)
        {
            HasHostTime = true;
            TargetOffset = sample;
            snapPending = true;
            return;
        }

        TargetOffset += SmoothingFactor * (sample - TargetOffset);
    }

    public float Advance(float localTime, bool isHost)
    {
        double step = lastLocal.HasValue ? Math.Max(0.0, localTime - lastLocal.Value) : 0.0;
        lastLocal = localTime;
        if (isHost) return Hold(localTime);

        if (snapPending)
        {
            snapPending = false;
            AppliedOffset = TargetOffset;
        }
        else if (HasHostTime)
        {
            double maxStep = SlewFraction * step;
            AppliedOffset += Math.Max(-maxStep, Math.Min(maxStep, TargetOffset - AppliedOffset));
        }

        return (float)(localTime + AppliedOffset);
    }

    // A host is the time source: it keeps the offset it has, so a promoted host continues from its own current water time.
    private float Hold(float localTime)
    {
        TargetOffset = AppliedOffset;
        snapPending = false;
        return (float)(localTime + AppliedOffset);
    }
}
