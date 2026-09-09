using System;

namespace Common.LiveTesting;

public enum ScreenshotCaptureStatus
{
    Pending,
    Complete,
    QualityRejected,
    TimedOut,
}

public sealed class ScreenshotCaptureAdvanceResult
{
    public ScreenshotCaptureStatus Status { get; }
    public bool Stable { get; }

    public ScreenshotCaptureAdvanceResult(ScreenshotCaptureStatus status, bool stable)
    {
        Status = status;
        Stable = stable;
    }
}

public sealed class ScreenshotCaptureState
{
    public object Gate { get; } = new object();
    public string CaptureId { get; }
    public string Path { get; }
    public DateTime CaptureRequestedUtc { get; }
    public int CaptureRequestEngineFrame { get; }
    public int MaximumObservations { get; }
    public TimeSpan CaptureTimeout { get; }
    public bool HasObservation { get; private set; }
    public long LastLength { get; private set; }
    public DateTime? LastWriteUtc { get; private set; }
    public BmpScreenshotObservation LastObservation { get; private set; }
    public DateTime LastObservationUtc { get; private set; }
    public int LastObservationEngineFrame { get; private set; }
    public int ObservationCount { get; private set; }
    public ScreenshotCaptureStatus Status { get; private set; }
    public bool Complete => Status == ScreenshotCaptureStatus.Complete;
    public bool QualityRejected => Status == ScreenshotCaptureStatus.QualityRejected;
    public bool TimedOut => Status == ScreenshotCaptureStatus.TimedOut;
    public BmpScreenshotEvidence Evidence { get; private set; }

    public ScreenshotCaptureState(
        string captureId,
        string path,
        DateTime captureRequestedUtc,
        int captureRequestEngineFrame,
        int maximumObservations,
        TimeSpan captureTimeout)
    {
        if (string.IsNullOrWhiteSpace(captureId)) throw new ArgumentException("A capture id is required.", nameof(captureId));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A screenshot path is required.", nameof(path));
        if (maximumObservations <= 0) throw new ArgumentOutOfRangeException(nameof(maximumObservations));
        if (captureTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(captureTimeout));

        CaptureId = captureId;
        Path = path;
        CaptureRequestedUtc = captureRequestedUtc;
        CaptureRequestEngineFrame = captureRequestEngineFrame;
        MaximumObservations = maximumObservations;
        CaptureTimeout = captureTimeout;
        Status = ScreenshotCaptureStatus.Pending;
    }

    public ScreenshotCaptureAdvanceResult Advance(
        DateTime observationUtc,
        int observationEngineFrame,
        BmpScreenshotObservation observation,
        IBmpScreenshotInspector inspector)
    {
        if (observation == null) throw new ArgumentNullException(nameof(observation));
        if (inspector == null) throw new ArgumentNullException(nameof(inspector));

        if (Complete) return new ScreenshotCaptureAdvanceResult(Status, true);
        if (QualityRejected || TimedOut) return new ScreenshotCaptureAdvanceResult(Status, false);

        if (ObservationCount >= MaximumObservations ||
            observationUtc - CaptureRequestedUtc > CaptureTimeout)
        {
            Status = ScreenshotCaptureStatus.TimedOut;
            return new ScreenshotCaptureAdvanceResult(Status, false);
        }

        ObservationCount++;
        bool eligibleForStability = observation.Exists &&
            observation.HeaderValid &&
            observation.LengthMatchesHeader &&
            observation.Length > 0 &&
            observation.IsFreshFor(CaptureRequestedUtc);
        bool stable = eligibleForStability &&
            HasObservation &&
            LastObservationEngineFrame != observationEngineFrame &&
            LastLength == observation.Length &&
            LastWriteUtc == observation.LastWriteUtc;

        HasObservation = eligibleForStability;
        LastLength = observation.Length;
        LastWriteUtc = observation.LastWriteUtc;
        LastObservation = observation;
        LastObservationUtc = observationUtc;
        LastObservationEngineFrame = observationEngineFrame;

        if (!stable) return new ScreenshotCaptureAdvanceResult(Status, false);

        if (!inspector.TryInspectStableFile(Path, observation, out var evidence))
        {
            HasObservation = false;
            return new ScreenshotCaptureAdvanceResult(Status, false);
        }

        Evidence = evidence;
        Status = evidence.PassesBasicQuality
            ? ScreenshotCaptureStatus.Complete
            : ScreenshotCaptureStatus.QualityRejected;
        return new ScreenshotCaptureAdvanceResult(Status, true);
    }
}
