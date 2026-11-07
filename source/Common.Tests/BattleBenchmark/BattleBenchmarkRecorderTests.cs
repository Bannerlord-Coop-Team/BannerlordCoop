using Common.BattleBenchmark;
using Common.Serialization;
using System.Diagnostics;
using System.Text.Json;

namespace Common.Tests.BattleBenchmark;

/// <summary>Verifies the battle benchmark window, its report contents and the JSON it writes.</summary>
public class BattleBenchmarkRecorderTests : IDisposable
{
    private static readonly long Second = Stopwatch.Frequency;
    private readonly string outputDirectory = Path.Combine(Path.GetTempPath(), "battle-benchmark-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeSampler sampler = new FakeSampler();
    private readonly FakeProbe probe = new FakeProbe();
    private readonly List<IGameThreadFrameObserver> attached = new List<IGameThreadFrameObserver>();
    private long now = 1_000 * Second;

    public void Dispose()
    {
        if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory, recursive: true);
    }

    private BattleBenchmarkRecorder CreateRecorder() =>
        new BattleBenchmarkRecorder(sampler, probe, new JsonFileIO(), () => now, attached.Add, observer => attached.Remove(observer));

    [Theory]
    [InlineData("bad id", 10, null, "run id")]
    [InlineData("run", 0.5, null, "window")]
    [InlineData("run", 901, null, "window")]
    [InlineData("run", 10, "relative/folder", "absolute")]
    public void TryStart_RejectsInvalidRequests(string runId, double seconds, string? directory, string expectedError)
    {
        var recorder = CreateRecorder();

        Assert.False(recorder.TryStart(runId, null, TimeSpan.FromSeconds(seconds), directory, out string error));
        Assert.Contains(expectedError, error);
        Assert.False(recorder.IsRecording);
        Assert.Empty(attached);
    }

    [Fact]
    public void TryStart_RejectsASecondWindowWhileRecording()
    {
        var recorder = CreateRecorder();
        Assert.True(recorder.TryStart("first", null, TimeSpan.FromSeconds(10), outputDirectory, out _));

        Assert.False(recorder.TryStart("second", null, TimeSpan.FromSeconds(10), outputDirectory, out string error));
        Assert.Contains("first", error);
        Assert.Single(attached);
    }

    [Fact]
    public void Window_EndsOnTheFirstFramePastItsLength_AndWritesTheReport()
    {
        var recorder = CreateRecorder();
        probe.State = new BattleBenchmarkMissionState { InMission = true, IsBattleHost = true, ControllerId = "PlayerOne", ActiveAgents = 980 };
        Assert.True(recorder.TryStart("field-01", "field", TimeSpan.FromSeconds(2), outputDirectory, out _));
        Assert.Same(recorder, Assert.Single(attached));

        // 10 ms frames whose drain takes 1 ms, until 2 s have passed.
        long frameStart = now;
        while (recorder.IsRecording)
        {
            frameStart += Second / 100;
            now = frameStart + (Second / 1000);
            recorder.OnFrame(frameStart, now);
        }

        Assert.Empty(attached);
        string path = Assert.Single(Directory.GetFiles(Path.Combine(outputDirectory, "field-01")));
        Assert.Equal($"battle-host-PlayerOne-{Process.GetCurrentProcess().Id}.json", Path.GetFileName(path));
        Assert.Equal(path, recorder.Status.LastReportPath);

        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = report.RootElement;
        Assert.Equal(BattleBenchmarkReport.CurrentSchema, root.GetProperty("Schema").GetString());
        Assert.Equal("field", root.GetProperty("Label").GetString());
        Assert.Equal("elapsed", root.GetProperty("Window").GetProperty("EndReason").GetString());
        JsonElement frames = root.GetProperty("Frames");
        // The first frame only anchors the interval, so 200 frames yield 199 intervals.
        Assert.Equal(199, frames.GetProperty("Count").GetInt32());
        Assert.Equal(10d, frames.GetProperty("P99Ms").GetDouble(), 3);
        JsonElement drain = root.GetProperty("Sections").GetProperty("GameThreadDrain");
        Assert.Equal(200, drain.GetProperty("Calls").GetInt64());
        Assert.Equal(1d, drain.GetProperty("MaxMs").GetDouble(), 3);
        Assert.Equal(100d, drain.GetProperty("MsPerSecond").GetDouble(), 1);
    }

    [Fact]
    public void Report_CountsSectionsTrafficAndMessagesOnlyWhileRecording()
    {
        var recorder = CreateRecorder();
        recorder.RecordSent("PlayerTwo", reliable: false, 900, 1020, relayed: false);
        recorder.RecordReceivedMessage(typeof(string));
        using (recorder.Measure(BattleBenchmarkSection.MovementSend)) now += Second;

        Assert.True(recorder.TryStart("run", null, TimeSpan.FromSeconds(10), outputDirectory, out _));
        using (recorder.Measure(BattleBenchmarkSection.MovementSend)) now += Second / 4;
        using (recorder.Measure(BattleBenchmarkSection.MovementSend)) now += Second / 4;
        recorder.RecordSent("PlayerTwo", reliable: false, 1000, 1020, relayed: false);
        recorder.RecordSent("PlayerTwo", reliable: false, 1000, 1020, relayed: false);
        recorder.RecordReliableMessageSent("PlayerTwo", 40);
        recorder.RecordSent("PlayerTwo", reliable: true, 400, 1020, relayed: false);
        recorder.RecordReceived("PlayerThree", reliable: false, 500);
        recorder.RecordReceivedMessage(typeof(int));
        recorder.RecordReceivedMessage(typeof(int));
        recorder.RecordReceivedMessage(typeof(string));
        now += Second / 2;

        BattleBenchmarkReport report = recorder.BuildReport(now, "stopped", sampler.Sample(), BattleBenchmarkMissionState.None);

        Assert.Equal(1d, report.Window.MeasuredSeconds, 6);
        BattleBenchmarkSectionTiming send = report.Sections[nameof(BattleBenchmarkSection.MovementSend)];
        Assert.Equal(2, send.Calls);
        Assert.Equal(500d, send.MsPerSecond, 6);
        Assert.Equal(250d, send.MaxMs, 6);
        Assert.Equal(new[] { "PlayerThree", "PlayerTwo" }, report.Routes.Select(route => route.Route));
        BattleBenchmarkTrafficCounts sent = report.Routes[1].Sent;
        Assert.Equal(2, sent.MovementDatagrams);
        Assert.Equal(2000d, sent.MovementBytesPerSecond, 6);
        Assert.Equal(1, sent.ReliableMessages);
        Assert.Equal(400, sent.ReliableBytes);
        Assert.Equal(2, report.Traffic.Sent.MovementDatagrams);
        Assert.Equal(1, report.Traffic.Received.MovementDatagrams);
        Assert.Equal(3, report.ReceivedMessages.Total);
        Assert.Equal(new[] { "Int32", "String" }, report.ReceivedMessages.ByType.Keys);
        Assert.Equal(2, report.ReceivedMessages.ByType["Int32"]);
    }

    [Fact]
    public void Report_CountsReliableFragmentsAndSplitsDirectFromRelayedBytes()
    {
        var recorder = CreateRecorder();
        Assert.True(recorder.TryStart("run", null, TimeSpan.FromSeconds(10), outputDirectory, out _));
        recorder.RecordSent("PlayerTwo", reliable: true, 1020, 1020, relayed: false);
        recorder.RecordSent("PlayerTwo", reliable: true, 2029, 1020, relayed: false);
        recorder.RecordSent("PlayerTwo", reliable: true, 1500, 1020, relayed: true);
        now += Second;

        BattleBenchmarkTrafficCounts sent = recorder.BuildReport(now, "stopped", sampler.Sample(), BattleBenchmarkMissionState.None)
            .Routes.Single().Sent;

        Assert.Equal(3, sent.ReliablePackets);
        Assert.Equal(1 + 3 + 2, sent.ReliableDatagrams);
        Assert.Equal(2, sent.OversizedReliablePackets);
        Assert.Equal(1020d + 2029d, sent.DirectReliableBytesPerSecond, 6);
        Assert.Equal(1500, sent.RelayedReliableBytes);
        Assert.Equal(2, sent.RelayedReliableDatagrams);
    }

    [Fact]
    public void Report_TotalsMatchTheirRows_WhileANetworkThreadKeepsWriting()
    {
        var recorder = new BattleBenchmarkRecorder(sampler, probe, new JsonFileIO(), Stopwatch.GetTimestamp, _ => { }, _ => { });
        Assert.True(recorder.TryStart("run", null, TimeSpan.FromSeconds(60), outputDirectory, out _));
        using var stop = new CancellationTokenSource();
        var writer = new Thread(() =>
        {
            for (int i = 0; !stop.IsCancellationRequested; i++)
            {
                recorder.RecordSent(i % 2 == 0 ? "PlayerTwo" : "PlayerThree", reliable: false, 100, 1020, relayed: false);
                recorder.RecordReceivedMessage(i % 2 == 0 ? typeof(int) : typeof(string));
            }
        });
        writer.Start();
        Thread.Sleep(50);

        BattleBenchmarkReport report = recorder.BuildReport(Stopwatch.GetTimestamp(), "stopped", default, BattleBenchmarkMissionState.None);
        stop.Cancel();
        writer.Join();

        Assert.Equal(report.Routes.Sum(route => route.Sent.MovementDatagrams), report.Traffic.Sent.MovementDatagrams);
        Assert.Equal(report.Routes.Sum(route => route.Sent.MovementBytes), report.Traffic.Sent.MovementBytes);
        Assert.Equal(report.ReceivedMessages.ByType.Values.Sum(), report.ReceivedMessages.Total);
    }

    // LiteNetLib 1.3.1 sends a reliable payload whole up to mtu - 4 bytes and otherwise in fragments of mtu - 4 - 6.
    [Theory]
    [InlineData(1, 1020, 1)]
    [InlineData(1020, 1020, 1)]
    [InlineData(1021, 1020, 2)]
    [InlineData(2028, 1020, 2)]
    [InlineData(2029, 1020, 3)]
    [InlineData(5000, 0, 1)]
    public void CountReliableDatagrams_MatchesLiteNetLibFragmentation(int bytes, int maxSinglePacketBytes, int expected)
    {
        Assert.Equal(expected, BattleBenchmarkRecorder.CountReliableDatagrams(bytes, maxSinglePacketBytes));
    }

    [Fact]
    public void Report_TakesGarbageCpuAndMemoryAsDifferencesOverTheWindow()
    {
        var recorder = CreateRecorder();
        sampler.Next = new BattleBenchmarkProcessSample(10, 4, 1, 1_000, 0, TimeSpan.FromSeconds(5), 100L << 20, 200L << 20, 150L << 20);
        Assert.True(recorder.TryStart("run", null, TimeSpan.FromSeconds(10), outputDirectory, out _));
        now += 2 * Second;
        var end = new BattleBenchmarkProcessSample(25, 6, 2, 9_001_000, 64L << 20, TimeSpan.FromSeconds(8), 300L << 20, 400L << 20, 350L << 20);

        BattleBenchmarkReport report = recorder.BuildReport(now, "stopped", end, BattleBenchmarkMissionState.None);

        Assert.Equal(15, report.Garbage.Gen0Collections);
        Assert.Equal(2, report.Garbage.Gen1Collections);
        Assert.Equal(1, report.Garbage.Gen2Collections);
        Assert.Equal(9_000_000, report.Garbage.AllocatedBytes);
        Assert.Equal(4_500_000d, report.Garbage.AllocatedBytesPerSecond, 6);
        Assert.Equal(150d, report.Cpu.PercentOfOneCore, 6);
        Assert.Equal(100d, report.Memory.WorkingSetStartMb, 6);
        Assert.Equal(400d, report.Memory.PeakWorkingSetMb, 6);
    }

    [Fact]
    public void Report_MarksAllocationsUnknownWhenTheRuntimeDoesNotCountThem()
    {
        var recorder = CreateRecorder();
        sampler.Next = new BattleBenchmarkProcessSample(0, 0, 0, -1, 0, TimeSpan.Zero, 0, 0, 0);
        Assert.True(recorder.TryStart("run", null, TimeSpan.FromSeconds(10), outputDirectory, out _));
        now += Second;

        BattleBenchmarkReport report = recorder.BuildReport(now, "stopped", sampler.Next, BattleBenchmarkMissionState.None);

        Assert.Equal(-1, report.Garbage.AllocatedBytes);
        Assert.Equal(-1d, report.Garbage.AllocatedBytesPerSecond);
    }

    [Fact]
    public void TryStop_WritesThePartialWindow_AndDisposeDetachesWithoutWriting()
    {
        var stopped = CreateRecorder();
        Assert.True(stopped.TryStart("stopped", null, TimeSpan.FromSeconds(60), outputDirectory, out _));
        now += 3 * Second;
        Assert.True(stopped.TryStop(out string path, out _));
        Assert.True(File.Exists(path));
        Assert.False(stopped.TryStop(out _, out string error));
        Assert.Contains("No benchmark", error);

        var disposed = CreateRecorder();
        Assert.True(disposed.TryStart("disposed", null, TimeSpan.FromSeconds(60), outputDirectory, out _));
        disposed.Dispose();
        Assert.False(disposed.IsRecording);
        Assert.Empty(attached);
        Assert.False(Directory.Exists(Path.Combine(outputDirectory, "disposed")));
    }

    [Fact]
    public void Window_ThatCannotBuildItsReport_EndsWithoutThrowingIntoTheGameLoop()
    {
        var recorder = CreateRecorder();
        Assert.True(recorder.TryStart("run", null, TimeSpan.FromSeconds(1), outputDirectory, out _));
        probe.Throw = true;

        now += 2 * Second;
        recorder.OnFrame(now, now);

        Assert.False(recorder.IsRecording);
        Assert.Empty(attached);
        Assert.Contains("probe failed", recorder.Status.LastError);
    }

    [Theory]
    [InlineData(0.50, 5)]
    [InlineData(0.95, 10)]
    [InlineData(0.99, 10)]
    [InlineData(0.10, 1)]
    public void Percentile_UsesTheNearestRank(double fraction, long expected)
    {
        long[] ascending = Enumerable.Range(1, 10).Select(value => (long)value).ToArray();

        Assert.Equal(expected, BattleBenchmarkRecorder.Percentile(ascending, fraction));
        Assert.Equal(0, BattleBenchmarkRecorder.Percentile(Array.Empty<long>(), fraction));
    }

    private sealed class FakeSampler : IBattleBenchmarkProcessSampler
    {
        public BattleBenchmarkProcessSample Next { get; set; }

        public void EnableAllocationMonitoring()
        {
        }

        public BattleBenchmarkProcessSample Sample() => Next;
    }

    private sealed class FakeProbe : IBattleBenchmarkMissionProbe
    {
        public BattleBenchmarkMissionState State { get; set; } = BattleBenchmarkMissionState.None;

        public bool Throw { get; set; }

        public BattleBenchmarkMissionState Capture() => Throw ? throw new InvalidOperationException("probe failed") : State;
    }
}
