#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Missions.Battles;

internal readonly struct BattleGuardAnimationFrame
{
    public int Channel { get; }
    public int AnimationIndex { get; }
    public float Progress { get; }
    public float Speed { get; }
    public bool IsCyclic { get; }

    public BattleGuardAnimationFrame(
        int channel,
        int animationIndex,
        float progress,
        float speed,
        bool isCyclic)
    {
        Channel = channel;
        AnimationIndex = animationIndex;
        Progress = progress;
        Speed = speed;
        IsCyclic = isCyclic;
    }
}

internal sealed class BattleGuardAnimationTrace
{
    private const float ProgressEpsilon = 0.001f;
    private const float NominalFrameSeconds = 1f / 60f;

    public int Channel { get; }
    public int AnimationIndex { get; }
    public int Samples { get; private set; }
    public int RunStarts { get; private set; }
    public int MaxRunSamples { get; private set; }
    public float DurationSeconds { get; private set; }
    public float MaxRunSeconds { get; private set; }
    public float ProgressMin { get; private set; } = -1f;
    public float ProgressMax { get; private set; } = -1f;
    public float ProgressSpan =>
        ProgressMin < 0f || ProgressMax < 0f
            ? 0f
            : ProgressMax - ProgressMin;
    public int ProgressAdvances { get; private set; }
    public int ProgressStalls { get; private set; }
    public int NonCyclicProgressResets { get; private set; }
    public float MaxNormalizedProgressStep { get; private set; }
    public float CurrentSpeed { get; private set; } = -1f;
    public float SpeedMin { get; private set; } = -1f;
    public float SpeedMax { get; private set; } = -1f;
    public float SpeedMean =>
        speedSamples == 0 ? -1f : speedTotal / speedSamples;

    private int lastFrame = -2;
    private int currentRunSamples;
    private float currentRunSeconds;
    private float previousProgress = -1f;
    private int speedSamples;
    private float speedTotal;

    public BattleGuardAnimationTrace(int channel, int animationIndex)
    {
        Channel = channel;
        AnimationIndex = animationIndex;
    }

    internal void Observe(
        int frame,
        float dt,
        float progress,
        float speed,
        bool isCyclic)
    {
        bool contiguous = lastFrame == frame - 1;
        if (!contiguous)
        {
            RunStarts++;
            currentRunSamples = 0;
            currentRunSeconds = 0f;
            previousProgress = -1f;
        }

        Samples++;
        currentRunSamples++;
        if (currentRunSamples > MaxRunSamples)
            MaxRunSamples = currentRunSamples;

        float elapsed = Math.Max(0f, dt);
        DurationSeconds += elapsed;
        currentRunSeconds += elapsed;
        if (currentRunSeconds > MaxRunSeconds)
            MaxRunSeconds = currentRunSeconds;

        if (progress >= 0f &&
            !float.IsNaN(progress) &&
            !float.IsInfinity(progress))
        {
            if (ProgressMin < 0f || progress < ProgressMin)
                ProgressMin = progress;
            if (ProgressMax < 0f || progress > ProgressMax)
                ProgressMax = progress;

            if (contiguous && previousProgress >= 0f)
                ObserveProgress(previousProgress, progress, elapsed, isCyclic);
            previousProgress = progress;
        }
        else
        {
            previousProgress = -1f;
        }

        if (!float.IsNaN(speed) && !float.IsInfinity(speed))
        {
            CurrentSpeed = speed;
            if (SpeedMin < 0f || speed < SpeedMin)
                SpeedMin = speed;
            if (SpeedMax < 0f || speed > SpeedMax)
                SpeedMax = speed;
            speedSamples++;
            speedTotal += speed;
        }

        lastFrame = frame;
    }

    private void ObserveProgress(
        float previous,
        float current,
        float dt,
        bool isCyclic)
    {
        float delta = current - previous;
        float measuredDelta = delta;
        if (delta < -ProgressEpsilon)
        {
            if (isCyclic && previous > 0.75f && current < 0.25f)
            {
                measuredDelta = (1f - previous) + current;
                ProgressAdvances++;
            }
            else
            {
                NonCyclicProgressResets++;
            }
        }
        else if (Math.Abs(delta) <= ProgressEpsilon)
        {
            ProgressStalls++;
        }
        else
        {
            ProgressAdvances++;
        }

        float normalizedStep =
            Math.Abs(measuredDelta) * NominalFrameSeconds /
            Math.Max(dt, NominalFrameSeconds / 4f);
        if (normalizedStep > MaxNormalizedProgressStep)
            MaxNormalizedProgressStep = normalizedStep;
    }
}

internal sealed class BattleGuardAnimationEvidence
{
    private readonly Dictionary<AnimationTraceKey, BattleGuardAnimationTrace>
        traces = new();
    private int frame;

    public IReadOnlyCollection<BattleGuardAnimationTrace> Traces =>
        traces.Values;

    public void ObserveFrame(
        float dt,
        BattleGuardAnimationFrame channel0,
        BattleGuardAnimationFrame channel1)
    {
        frame++;
        Observe(dt, channel0);
        Observe(dt, channel1);
    }

    public bool TryGetTrace(
        int channel,
        int animationIndex,
        out BattleGuardAnimationTrace trace)
    {
        return traces.TryGetValue(
            new AnimationTraceKey(channel, animationIndex),
            out trace);
    }

    public string GetToken()
    {
        if (traces.Count == 0)
            return "none";

        var ordered = new List<BattleGuardAnimationTrace>(traces.Values);
        ordered.Sort(
            (left, right) =>
            {
                int channelComparison = left.Channel.CompareTo(right.Channel);
                return channelComparison != 0
                    ? channelComparison
                    : left.AnimationIndex.CompareTo(right.AnimationIndex);
            });

        var tokens = new List<string>(ordered.Count);
        foreach (BattleGuardAnimationTrace trace in ordered)
        {
            tokens.Add(
                string.Join(
                    ":",
                    trace.Channel,
                    trace.AnimationIndex,
                    trace.Samples,
                    trace.RunStarts,
                    trace.MaxRunSamples,
                    Format(trace.DurationSeconds),
                    Format(trace.MaxRunSeconds),
                    Format(trace.ProgressMin),
                    Format(trace.ProgressMax),
                    Format(trace.ProgressSpan),
                    trace.ProgressAdvances,
                    trace.ProgressStalls,
                    trace.NonCyclicProgressResets,
                    Format(trace.MaxNormalizedProgressStep),
                    Format(trace.CurrentSpeed),
                    Format(trace.SpeedMin),
                    Format(trace.SpeedMax),
                    Format(trace.SpeedMean)));
        }

        return string.Join(";", tokens);
    }

    private void Observe(float dt, BattleGuardAnimationFrame sample)
    {
        if (sample.AnimationIndex < 0)
            return;

        var key = new AnimationTraceKey(
            sample.Channel,
            sample.AnimationIndex);
        if (!traces.TryGetValue(key, out BattleGuardAnimationTrace trace))
        {
            trace = new BattleGuardAnimationTrace(
                sample.Channel,
                sample.AnimationIndex);
            traces.Add(key, trace);
        }

        trace.Observe(
            frame,
            dt,
            sample.Progress,
            sample.Speed,
            sample.IsCyclic);
    }

    private static string Format(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private readonly struct AnimationTraceKey : IEquatable<AnimationTraceKey>
    {
        private readonly int channel;
        private readonly int animationIndex;

        public AnimationTraceKey(int channel, int animationIndex)
        {
            this.channel = channel;
            this.animationIndex = animationIndex;
        }

        public bool Equals(AnimationTraceKey other) =>
            channel == other.channel &&
            animationIndex == other.animationIndex;

        public override bool Equals(object obj) =>
            obj is AnimationTraceKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (channel * 397) ^ animationIndex;
            }
        }
    }
}

internal sealed class BattleGuardSpeedEvidence
{
    private const float RecentWindowSeconds = 2f;
    private const float MaximumPlateauSpread = 0.15f;
    private const float MaximumPlateauSlope = 0.35f;
    private readonly List<float> allSpeeds = new();
    private readonly List<TimedSpeed> recentSpeeds = new();
    private float recentDuration;

    public int Samples => allSpeeds.Count;
    public int RecentSamples => recentSpeeds.Count;
    public float Current { get; private set; }
    public float Peak { get; private set; }
    public float Median => GetMedian(allSpeeds);
    public float RecentMedian
    {
        get
        {
            var speeds = new List<float>(recentSpeeds.Count);
            foreach (TimedSpeed sample in recentSpeeds)
                speeds.Add(sample.Speed);
            return GetMedian(speeds);
        }
    }
    public float RecentSpread
    {
        get
        {
            float median = RecentMedian;
            if (median <= 0f || recentSpeeds.Count == 0)
                return 0f;

            float minimum = recentSpeeds[0].Speed;
            float maximum = minimum;
            foreach (TimedSpeed sample in recentSpeeds)
            {
                if (sample.Speed < minimum)
                    minimum = sample.Speed;
                if (sample.Speed > maximum)
                    maximum = sample.Speed;
            }
            return (maximum - minimum) / median;
        }
    }
    public float RecentSlope
    {
        get
        {
            if (recentSpeeds.Count < 2 || recentDuration <= 0f)
                return 0f;
            return
                (recentSpeeds[recentSpeeds.Count - 1].Speed -
                 recentSpeeds[0].Speed) /
                recentDuration;
        }
    }
    public bool PlateauReady =>
        recentDuration >= RecentWindowSeconds * 0.9f &&
        recentSpeeds.Count >= 20 &&
        RecentMedian > 0.5f &&
        RecentSpread <= MaximumPlateauSpread &&
        Math.Abs(RecentSlope) <= MaximumPlateauSlope;

    public void Observe(float speed, float elapsed)
    {
        if (elapsed <= 0f ||
            speed < 0f ||
            float.IsNaN(speed) ||
            float.IsInfinity(speed))
        {
            return;
        }

        Current = speed;
        if (speed > Peak)
            Peak = speed;
        allSpeeds.Add(speed);
        recentSpeeds.Add(new TimedSpeed(speed, elapsed));
        recentDuration += elapsed;

        while (recentSpeeds.Count > 1 &&
               recentDuration - recentSpeeds[0].Elapsed >=
               RecentWindowSeconds)
        {
            recentDuration -= recentSpeeds[0].Elapsed;
            recentSpeeds.RemoveAt(0);
        }
    }

    private static float GetMedian(List<float> values)
    {
        if (values.Count == 0)
            return 0f;

        var sorted = new List<float>(values);
        sorted.Sort();
        int middle = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2f
            : sorted[middle];
    }

    private readonly struct TimedSpeed
    {
        public float Speed { get; }
        public float Elapsed { get; }

        public TimedSpeed(float speed, float elapsed)
        {
            Speed = speed;
            Elapsed = elapsed;
        }
    }
}

internal sealed class BattleGuardContinuityEvidence
{
    public int Samples { get; private set; }
    public int ExactSamples { get; private set; }
    public int Interruptions { get; private set; }
    public float ExactDurationSeconds { get; private set; }
    public float MaxExactRunSeconds { get; private set; }
    public float ExactPercent =>
        Samples == 0 ? 0f : 100f * ExactSamples / Samples;

    private bool wasExact;
    private bool hasSeenExact;
    private float currentExactRunSeconds;

    public void Observe(bool exact, float dt)
    {
        Samples++;
        if (exact)
        {
            ExactSamples++;
            float elapsed = Math.Max(0f, dt);
            ExactDurationSeconds += elapsed;
            currentExactRunSeconds = wasExact
                ? currentExactRunSeconds + elapsed
                : elapsed;
            if (currentExactRunSeconds > MaxExactRunSeconds)
                MaxExactRunSeconds = currentExactRunSeconds;
            hasSeenExact = true;
        }
        else
        {
            if (wasExact && hasSeenExact)
                Interruptions++;
            currentExactRunSeconds = 0f;
        }

        wasExact = exact;
    }
}

internal sealed class BattleGuardReactionEvidence
{
    private const float MissingVisualGraceSeconds = 0.05f;
    private const float TerminalProgress = 0.70f;

    public bool HasStarted { get; private set; }
    public bool Active { get; private set; }
    public bool Completed { get; private set; }
    public bool Interrupted { get; private set; }
    public int Channel { get; private set; } = -1;
    public int ActionIndex { get; private set; } = -1;
    public int AnimationIndex { get; private set; } = -1;
    public float OnsetSpeed { get; private set; } = -1f;
    public float VisualDurationSeconds { get; private set; }
    public float LastVisualProgress { get; private set; } = -1f;
    public float MaxVisualProgress { get; private set; } = -1f;

    private bool hasExactVisual;
    private float missingVisualSeconds;

    public void Observe(
        bool receivedReactionActive,
        bool exactVisual,
        bool returnedToExactGuard,
        int channel,
        int actionIndex,
        int animationIndex,
        float visualProgress,
        float speed,
        float dt)
    {
        if (Completed || Interrupted)
            return;

        if (!HasStarted && receivedReactionActive)
        {
            HasStarted = true;
            Active = true;
            Channel = channel;
            ActionIndex = actionIndex;
            AnimationIndex = animationIndex;
            OnsetSpeed = speed;
        }
        else if (HasStarted &&
                 receivedReactionActive &&
                 (channel != Channel ||
                  actionIndex != ActionIndex))
        {
            Interrupted = true;
            Active = false;
            return;
        }

        if (!HasStarted)
            return;
        if (exactVisual)
        {
            Active = true;
            if (!hasExactVisual)
                AnimationIndex = animationIndex;
            hasExactVisual = true;
            missingVisualSeconds = 0f;
            VisualDurationSeconds += Math.Max(0f, dt);
            LastVisualProgress = visualProgress;
            if (visualProgress > MaxVisualProgress)
                MaxVisualProgress = visualProgress;
            return;
        }

        bool reachedTerminal =
            MaxVisualProgress >= TerminalProgress;
        if (hasExactVisual && reachedTerminal)
        {
            Completed = true;
            Active = false;
            return;
        }
        if (hasExactVisual && returnedToExactGuard)
        {
            Interrupted = true;
            Active = false;
            return;
        }

        missingVisualSeconds += Math.Max(0f, dt);
        if (missingVisualSeconds > MissingVisualGraceSeconds)
        {
            Interrupted = true;
            Active = false;
            return;
        }
        Active = receivedReactionActive || hasExactVisual;
    }
}

internal sealed class BattleGuardReplayEvidence
{
    private const float ProgressEpsilon = 0.001f;

    public int PairedFrames { get; private set; }
    public int AnimationChanges { get; private set; }
    public int ProgressRewinds { get; private set; }
    public float MaxProgressDelta { get; private set; }
    public float MaxSpeedDelta { get; private set; }

    private BattleGuardAnimationFrame preChannel0;
    private BattleGuardAnimationFrame preChannel1;
    private bool hasPreFrame;

    public void CapturePre(
        BattleGuardAnimationFrame channel0,
        BattleGuardAnimationFrame channel1)
    {
        preChannel0 = channel0;
        preChannel1 = channel1;
        hasPreFrame = true;
    }

    public void ObservePost(
        BattleGuardAnimationFrame channel0,
        BattleGuardAnimationFrame channel1)
    {
        if (!hasPreFrame)
            return;

        PairedFrames++;
        Compare(preChannel0, channel0);
        Compare(preChannel1, channel1);
        hasPreFrame = false;
    }

    public void ClearPre()
    {
        hasPreFrame = false;
    }

    private void Compare(
        BattleGuardAnimationFrame before,
        BattleGuardAnimationFrame after)
    {
        if (before.AnimationIndex != after.AnimationIndex)
        {
            AnimationChanges++;
            return;
        }
        if (before.AnimationIndex < 0)
            return;

        float progressDelta = after.Progress - before.Progress;
        if (progressDelta < -ProgressEpsilon)
            ProgressRewinds++;
        float absoluteProgressDelta = Math.Abs(progressDelta);
        if (absoluteProgressDelta > MaxProgressDelta)
            MaxProgressDelta = absoluteProgressDelta;

        float speedDelta = Math.Abs(after.Speed - before.Speed);
        if (speedDelta > MaxSpeedDelta)
            MaxSpeedDelta = speedDelta;
    }
}

internal sealed class BattleGuardVisualRootEvidence
{
    public float CurrentPositionSpeed { get; private set; }
    public float PositionVisualDelta { get; private set; }
    public float MaxPositionVisualDelta { get; private set; }
    public float MaxVisualRootStep { get; private set; }
    public float MaxVisualRootStepRate { get; private set; }

    private bool hasPreviousPosition;
    private float previousPositionX;
    private float previousPositionY;
    private float previousVisualX;
    private float previousVisualY;
    private float previousVisualZ;

    public void Observe(
        float positionX,
        float positionY,
        float positionZ,
        float visualX,
        float visualY,
        float visualZ,
        float dt)
    {
        PositionVisualDelta = Distance(
            positionX,
            positionY,
            positionZ,
            visualX,
            visualY,
            visualZ);
        if (PositionVisualDelta > MaxPositionVisualDelta)
            MaxPositionVisualDelta = PositionVisualDelta;

        if (hasPreviousPosition)
        {
            float positionStep = Distance2D(
                previousPositionX,
                previousPositionY,
                positionX,
                positionY);
            CurrentPositionSpeed =
                dt > 0f ? positionStep / dt : 0f;

            float visualStep = Distance(
                previousVisualX,
                previousVisualY,
                previousVisualZ,
                visualX,
                visualY,
                visualZ);
            if (visualStep > MaxVisualRootStep)
                MaxVisualRootStep = visualStep;
            float visualStepRate = dt > 0f ? visualStep / dt : 0f;
            if (visualStepRate > MaxVisualRootStepRate)
                MaxVisualRootStepRate = visualStepRate;
        }

        previousPositionX = positionX;
        previousPositionY = positionY;
        previousVisualX = visualX;
        previousVisualY = visualY;
        previousVisualZ = visualZ;
        hasPreviousPosition = true;
    }

    private static float Distance(
        float leftX,
        float leftY,
        float leftZ,
        float rightX,
        float rightY,
        float rightZ)
    {
        float x = rightX - leftX;
        float y = rightY - leftY;
        float z = rightZ - leftZ;
        return (float)Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    private static float Distance2D(
        float leftX,
        float leftY,
        float rightX,
        float rightY)
    {
        float x = rightX - leftX;
        float y = rightY - leftY;
        return (float)Math.Sqrt((x * x) + (y * y));
    }
}
#endif
