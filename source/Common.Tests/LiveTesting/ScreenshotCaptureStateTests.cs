using Common.LiveTesting;

namespace Common.Tests.LiveTesting;

public sealed class ScreenshotCaptureStateTests
{
    private static readonly DateTime RequestedUtc = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Advance_RequiresMatchingFreshObservationsOnDifferentFrames()
    {
        var inspector = new StubInspector(true, PassingEvidence());
        ScreenshotCaptureState capture = CreateCapture();
        BmpScreenshotObservation observation = ValidObservation(100, RequestedUtc.AddSeconds(1));

        ScreenshotCaptureAdvanceResult first = capture.Advance(
            RequestedUtc.AddSeconds(2), 11, observation, inspector);
        ScreenshotCaptureAdvanceResult sameFrame = capture.Advance(
            RequestedUtc.AddSeconds(3), 11, observation, inspector);
        ScreenshotCaptureAdvanceResult nextFrame = capture.Advance(
            RequestedUtc.AddSeconds(4), 12, observation, inspector);
        ScreenshotCaptureAdvanceResult afterCompletion = capture.Advance(
            RequestedUtc.AddSeconds(5), 13, observation, inspector);

        Assert.Equal(ScreenshotCaptureStatus.Pending, first.Status);
        Assert.False(first.Stable);
        Assert.Equal(ScreenshotCaptureStatus.Pending, sameFrame.Status);
        Assert.False(sameFrame.Stable);
        Assert.Equal(ScreenshotCaptureStatus.Complete, nextFrame.Status);
        Assert.True(nextFrame.Stable);
        Assert.Equal(ScreenshotCaptureStatus.Complete, afterCompletion.Status);
        Assert.True(afterCompletion.Stable);
        Assert.Equal(1, inspector.StableInspectionCount);
    }

    [Fact]
    public void Advance_FileChangeRestartsStabilityWindow()
    {
        var inspector = new StubInspector(true, PassingEvidence(101));
        ScreenshotCaptureState capture = CreateCapture();
        BmpScreenshotObservation firstFile = ValidObservation(100, RequestedUtc.AddSeconds(1));
        BmpScreenshotObservation changedFile = ValidObservation(101, RequestedUtc.AddSeconds(2));

        capture.Advance(RequestedUtc.AddSeconds(2), 20, firstFile, inspector);
        ScreenshotCaptureAdvanceResult changed = capture.Advance(
            RequestedUtc.AddSeconds(3), 21, changedFile, inspector);
        ScreenshotCaptureAdvanceResult unchanged = capture.Advance(
            RequestedUtc.AddSeconds(4), 22, changedFile, inspector);

        Assert.Equal(ScreenshotCaptureStatus.Pending, changed.Status);
        Assert.False(changed.Stable);
        Assert.Equal(ScreenshotCaptureStatus.Complete, unchanged.Status);
        Assert.True(unchanged.Stable);
        Assert.Equal(1, inspector.StableInspectionCount);
    }

    [Fact]
    public void Advance_InspectionRaceRequiresTwoNewObservations()
    {
        var inspector = new StubInspector(false, null);
        inspector.EnqueueResult(true, PassingEvidence());
        ScreenshotCaptureState capture = CreateCapture();
        BmpScreenshotObservation observation = ValidObservation(100, RequestedUtc.AddSeconds(1));

        capture.Advance(RequestedUtc.AddSeconds(2), 30, observation, inspector);
        ScreenshotCaptureAdvanceResult raced = capture.Advance(
            RequestedUtc.AddSeconds(3), 31, observation, inspector);
        ScreenshotCaptureAdvanceResult reset = capture.Advance(
            RequestedUtc.AddSeconds(4), 32, observation, inspector);
        ScreenshotCaptureAdvanceResult completed = capture.Advance(
            RequestedUtc.AddSeconds(5), 33, observation, inspector);

        Assert.Equal(ScreenshotCaptureStatus.Pending, raced.Status);
        Assert.False(raced.Stable);
        Assert.Equal(ScreenshotCaptureStatus.Pending, reset.Status);
        Assert.False(reset.Stable);
        Assert.Equal(ScreenshotCaptureStatus.Complete, completed.Status);
        Assert.True(completed.Stable);
        Assert.Equal(2, inspector.StableInspectionCount);
    }

    [Fact]
    public void Advance_QualityFailureIsTerminal()
    {
        var inspector = new StubInspector(true, RejectedEvidence());
        ScreenshotCaptureState capture = CreateCapture();
        BmpScreenshotObservation observation = ValidObservation(100, RequestedUtc.AddSeconds(1));

        capture.Advance(RequestedUtc.AddSeconds(2), 40, observation, inspector);
        ScreenshotCaptureAdvanceResult rejected = capture.Advance(
            RequestedUtc.AddSeconds(3), 41, observation, inspector);
        ScreenshotCaptureAdvanceResult repeated = capture.Advance(
            RequestedUtc.AddSeconds(4), 42, observation, inspector);

        Assert.Equal(ScreenshotCaptureStatus.QualityRejected, rejected.Status);
        Assert.True(rejected.Stable);
        Assert.Equal(ScreenshotCaptureStatus.QualityRejected, repeated.Status);
        Assert.False(repeated.Stable);
        Assert.True(capture.QualityRejected);
        Assert.Equal(BmpScreenshotQualityVerdict.AllBlack, capture.Evidence.QualityVerdict);
        Assert.Equal(1, inspector.StableInspectionCount);
    }

    [Fact]
    public void Advance_ObservationLimitTimesOutBeforeAnotherInspection()
    {
        var inspector = new StubInspector(true, PassingEvidence());
        ScreenshotCaptureState capture = CreateCapture(maximumObservations: 2);

        capture.Advance(RequestedUtc.AddSeconds(1), 50, BmpScreenshotObservation.Missing, inspector);
        capture.Advance(RequestedUtc.AddSeconds(2), 51, BmpScreenshotObservation.Missing, inspector);
        ScreenshotCaptureAdvanceResult timedOut = capture.Advance(
            RequestedUtc.AddSeconds(3), 52, BmpScreenshotObservation.Missing, inspector);

        Assert.Equal(ScreenshotCaptureStatus.TimedOut, timedOut.Status);
        Assert.True(capture.TimedOut);
        Assert.Equal(2, capture.ObservationCount);
        Assert.Equal(0, inspector.StableInspectionCount);
    }

    [Fact]
    public void Advance_ElapsedDeadlineTimesOutBeforeFirstObservation()
    {
        var inspector = new StubInspector(true, PassingEvidence());
        ScreenshotCaptureState capture = CreateCapture(captureTimeout: TimeSpan.FromSeconds(10));

        ScreenshotCaptureAdvanceResult timedOut = capture.Advance(
            RequestedUtc.AddSeconds(11),
            60,
            ValidObservation(100, RequestedUtc.AddSeconds(1)),
            inspector);

        Assert.Equal(ScreenshotCaptureStatus.TimedOut, timedOut.Status);
        Assert.Equal(0, capture.ObservationCount);
        Assert.Equal(0, inspector.StableInspectionCount);
    }

    private static ScreenshotCaptureState CreateCapture(
        int maximumObservations = 10,
        TimeSpan? captureTimeout = null)
    {
        return new ScreenshotCaptureState(
            "capture-id",
            "C:\\captures\\frame.bmp",
            RequestedUtc,
            10,
            maximumObservations,
            captureTimeout ?? TimeSpan.FromMinutes(1));
    }

    private static BmpScreenshotObservation ValidObservation(long length, DateTime lastWriteUtc)
    {
        return new BmpScreenshotObservation(
            true,
            true,
            length,
            length,
            2,
            2,
            24,
            lastWriteUtc);
    }

    private static BmpScreenshotEvidence PassingEvidence(long length = 100)
    {
        return new BmpScreenshotEvidence(
            length,
            length,
            true,
            2,
            2,
            24,
            new string('a', 64),
            BmpScreenshotQualityVerdict.NonUniformPixelData,
            "non-uniform");
    }

    private static BmpScreenshotEvidence RejectedEvidence()
    {
        return new BmpScreenshotEvidence(
            100,
            100,
            true,
            2,
            2,
            24,
            new string('b', 64),
            BmpScreenshotQualityVerdict.AllBlack,
            "all black");
    }

    private sealed class StubInspector : IBmpScreenshotInspector
    {
        private readonly Queue<(bool Success, BmpScreenshotEvidence? Evidence)> results = new();

        public int StableInspectionCount { get; private set; }

        public StubInspector(bool success, BmpScreenshotEvidence? evidence)
        {
            results.Enqueue((success, evidence));
        }

        public void EnqueueResult(bool success, BmpScreenshotEvidence? evidence)
        {
            results.Enqueue((success, evidence));
        }

        public BmpScreenshotObservation ObserveFile(string path)
        {
            throw new NotSupportedException();
        }

        public bool TryInspectStableFile(
            string path,
            BmpScreenshotObservation expectedObservation,
            out BmpScreenshotEvidence evidence)
        {
            StableInspectionCount++;
            (bool success, BmpScreenshotEvidence? result) = results.Dequeue();
            evidence = result!;
            return success;
        }

        public BmpScreenshotEvidence Inspect(byte[] bytes)
        {
            throw new NotSupportedException();
        }
    }
}
