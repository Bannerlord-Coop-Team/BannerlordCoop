using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Battles;

/// <summary>
/// Turns the battle host's water time samples into this client's water time: it never runs backwards, and once the local
/// clock moves a forward gap is slewed, jumping only on the first adoption or after a host change.
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

    /// <summary>After a host change, lets the next host sample that is far enough ahead jump at once.</summary>
    void RearmAdoption();

    /// <summary>On the host: jumps forward to a peer's time that is far enough ahead, so a lagging promoted host catches up.</summary>
    bool AdoptIfAhead(float time, float localTime);

    /// <summary>Advances to <paramref name="localTime"/> and returns the water time to write; a host holds its own offset.</summary>
    float Advance(float localTime, bool isHost);
}

/// <inheritdoc cref="IBattleWaterTimeEstimator"/>
public class BattleWaterTimeEstimator : IBattleWaterTimeEstimator
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleWaterTimeEstimator>();

    // Weight of a new windowed offset in the smoothed target; one sample a second settles in a few seconds.
    internal const double SmoothingFactor = 0.3;
    // The applied offset moves at most this fraction of the local advance: waves run at 75% to 125% speed, never backwards.
    internal const double SlewFraction = 0.25;
    // A forward gap beyond this (20 water units = 2 s of mission time) is adopted at once, or caught up faster mid-battle.
    internal const double SnapThreshold = 20.0;
    // Catching up a persistent gap runs the waves at up to double speed, so a stalled clock recovers in as long as it stood.
    internal const double CatchUpFraction = 1.0;
    // Transit delay only ever makes an offset look smaller, so the window's largest offset is the best estimate.
    internal const int SampleWindow = 3;

    private readonly Queue<double> window = new Queue<double>();
    private double? lastLocal;
    private bool begun;
    // The next host sample jumps at once when far enough ahead: the first one, and the first after a host change.
    private bool adoptionArmed;
    private bool jumpPending;
    // A persistent forward gap slews at the catch-up rate until the applied offset reaches the target.
    private bool catchingUp;
    // Whether the local clock has moved since Begin; until it does no water has run, so a forward gap can still jump.
    private bool hasLocalProgress;

    public bool HasHostTime { get; private set; }

    public double TargetOffset { get; private set; }

    public double AppliedOffset { get; private set; }

    public float Begin(float localTime, float startTime)
    {
        begun = true;
        lastLocal = localTime;
        // The latest host time that arrived during loading is the start, before any frame shows the battle's water.
        bool freshHostTime = window.Count > 0;
        if (freshHostTime) AppliedOffset = TargetOffset = window.Max();
        else AppliedOffset = TargetOffset = (double)startTime - localTime;
        adoptionArmed = !freshHostTime;
        return (float)(localTime + AppliedOffset);
    }

    public void AddHostSample(float hostTime, float localTime)
    {
        double offset = (double)hostTime - localTime;
        window.Enqueue(offset);
        if (window.Count > SampleWindow) window.Dequeue();

        TargetOffset = HasHostTime ? TargetOffset + (SmoothingFactor * (window.Max() - TargetOffset)) : offset;
        HasHostTime = true;
        if (!begun) return;

        if (adoptionArmed)
        {
            adoptionArmed = false;
            if (offset - AppliedOffset > SnapThreshold) JumpTo(offset);
            return;
        }

        if (window.Count == SampleWindow && window.Min() - AppliedOffset > SnapThreshold) CatchUpTo(window.Max());
    }

    public void RearmAdoption()
    {
        adoptionArmed = true;
        window.Clear();
    }

    public bool AdoptIfAhead(float time, float localTime)
    {
        double offset = (double)time - localTime;
        if (offset - AppliedOffset <= SnapThreshold) return false;

        AppliedOffset = TargetOffset = offset;
        return true;
    }

    public float Advance(float localTime, bool isHost)
    {
        double step = lastLocal.HasValue ? Math.Max(0.0, localTime - lastLocal.Value) : 0.0;
        lastLocal = localTime;
        hasLocalProgress |= step > 0.0;
        if (isHost) return Hold(localTime);

        if (jumpPending && TargetOffset > AppliedOffset)
        {
            AppliedOffset = TargetOffset;
        }
        else
        {
            double maxForward = (catchingUp ? CatchUpFraction : SlewFraction) * step;
            AppliedOffset += Math.Max(-SlewFraction * step, Math.Min(maxForward, TargetOffset - AppliedOffset));
        }
        jumpPending = false;
        catchingUp &= AppliedOffset < TargetOffset;
        return (float)(localTime + AppliedOffset);
    }

    // Sets a forward target the next advance jumps to.
    private void JumpTo(double offset)
    {
        if (hasLocalProgress)
            Logger.Information("[NavalSync] Water time jumped forward {Gap} to the battle host's", offset - AppliedOffset);
        TargetOffset = offset;
        jumpPending = true;
    }

    // Sets a forward target the advances catch up to at the faster rate; before the local clock has moved it jumps instead.
    private void CatchUpTo(double offset)
    {
        if (!hasLocalProgress)
        {
            JumpTo(offset);
            return;
        }

        if (!catchingUp)
            Logger.Information("[NavalSync] Water time {Gap} behind the battle host, catching up", offset - AppliedOffset);
        TargetOffset = offset;
        catchingUp = true;
    }

    // A host is the time source: it keeps the offset it has, so a promoted host continues from its own current water time.
    private float Hold(float localTime)
    {
        TargetOffset = AppliedOffset;
        jumpPending = false;
        catchingUp = false;
        window.Clear();
        return (float)(localTime + AppliedOffset);
    }
}
