using Common.BattleBenchmark;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using LiteNetLib;
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

    [Fact]
    public void TryStart_RejectsASecondWindowWhileRecording()
    {
        var recorder = CreateRecorder();
        Assert.True(recorder.TryStart("first", null, TimeSpan.FromSeconds(10), outputDirectory, out _));

        Assert.False(recorder.TryStart("second", null, TimeSpan.FromSeconds(10), outputDirectory, out string error));
        Assert.Contains("first", error);
        Assert.Single(attached);
    }

    [Theory]
    [InlineData(@"C:bench")]
    [InlineData(@"\bench")]
    [InlineData("bench")]
    public void TryStart_RejectsAnOutputFolderThatIsNotFullyQualified(string folder)
    {
        var recorder = CreateRecorder();

        Assert.False(recorder.TryStart("run", null, TimeSpan.FromSeconds(10), folder, out string error));
        Assert.Contains("absolute", error);
        Assert.False(recorder.IsRecording);
        Assert.Empty(attached);
    }

    [Fact]
    public void TryStart_CreatesTheRunFolder_OrFailsBeforeTheWindowWhenItCannot()
    {
        // A file where the output folder should be makes the run folder impossible to create.
        Directory.CreateDirectory(outputDirectory);
        string blocked = Path.Combine(outputDirectory, "blocked");
        File.WriteAllText(blocked, string.Empty);
        var recorder = CreateRecorder();

        Assert.False(recorder.TryStart("run", null, TimeSpan.FromSeconds(10), blocked, out string error));
        Assert.Contains(blocked, error);
        Assert.False(recorder.IsRecording);
        Assert.Empty(attached);

        Assert.True(recorder.TryStart("run", null, TimeSpan.FromSeconds(10), outputDirectory, out _));
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "run")));
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
    public void Report_CountsBytesPerTypeAndOnlyTheFramingOfAReceivedAggregate()
    {
        var recorder = CreateRecorder();
        recorder.RecordTypeSent(typeof(int), 999);

        Assert.True(recorder.TryStart("run", null, TimeSpan.FromSeconds(10), outputDirectory, out _));
        recorder.RecordTypeSent(typeof(int), 100);
        recorder.RecordTypeSent(typeof(int), 300);
        recorder.RecordTypeSent(typeof(string), 50);
        // Only the 22 bytes of framing around the 30 and 20 byte messages; the handler counts those as it unpacks them.
        recorder.RecordPayloadReceived(new AggregateMessagePacket(new[] { new byte[30], new byte[20] }), 72);
        recorder.RecordTypeReceived(typeof(int), 30);
        recorder.RecordTypeReceived(typeof(int), 20);
        recorder.RecordPayloadReceived("bare", 40);
        now += 2 * Second;

        BattleBenchmarkMessageTraffic traffic = recorder.BuildReport(now, "stopped", sampler.Sample(), BattleBenchmarkMissionState.None)
            .MessageTraffic;

        Assert.Equal(new[] { "Int32", "String" }, traffic.Sent.ByType.Keys);
        BattleBenchmarkByteCounts sentInts = traffic.Sent.ByType["Int32"];
        Assert.Equal(2, sentInts.Count);
        Assert.Equal(400, sentInts.Bytes);
        Assert.Equal(200d, sentInts.BytesPerSecond, 6);
        Assert.Equal(1d, sentInts.CountPerSecond, 6);
        Assert.Equal(200d, sentInts.AverageBytes, 6);
        Assert.Equal(450, traffic.Sent.Total.Bytes);
        Assert.Equal(new[] { "Int32", "String", "AggregateMessagePacket" }, traffic.Received.ByType.Keys);
        Assert.Equal(22, traffic.Received.ByType["AggregateMessagePacket"].Bytes);
        Assert.Equal(72 + 40, traffic.Received.Total.Bytes);
    }

    [Fact]
    public void Report_CountsReceivedReliableMessagesLikeTheSentOnes()
    {
        var recorder = CreateRecorder();
        recorder.RecordReliableMessagesReceived("PlayerTwo", new ReceivedMessage(), 99);

        Assert.True(recorder.TryStart("run", null, TimeSpan.FromSeconds(10), outputDirectory, out _));
        recorder.RecordReliableMessagesReceived("PlayerTwo", new AggregateMessagePacket(new[] { new byte[30], new byte[20], new byte[10] }), 72);
        recorder.RecordReliableMessagesReceived("PlayerTwo", new ReceivedMessage(), 40);
        recorder.RecordReliableMessagesReceived("PlayerTwo", new ReceivedPacket(DeliveryMethod.ReliableUnordered), 300);
        recorder.RecordReliableMessagesReceived("PlayerTwo", new ReceivedPacket(DeliveryMethod.Unreliable), 500);
        now += Second;

        BattleBenchmarkReport report = recorder.BuildReport(now, "stopped", sampler.Sample(), BattleBenchmarkMissionState.None);

        BattleBenchmarkTrafficCounts received = Assert.Single(report.Routes).Received;
        Assert.Equal(5, received.ReliableMessages);
        Assert.Equal(30 + 20 + 10 + 40 + 300, received.ReliableMessageBytes);
        Assert.Equal(5d, received.ReliableMessagesPerSecond, 6);
        Assert.Equal(5, report.Traffic.Received.ReliableMessages);
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
        using var writing = new ManualResetEventSlim();
        var writer = new Thread(() =>
        {
            for (int i = 0; !stop.IsCancellationRequested; i++)
            {
                recorder.RecordSent(i % 2 == 0 ? "PlayerTwo" : "PlayerThree", reliable: false, 100, 1020, relayed: false);
                recorder.RecordReceivedMessage(i % 2 == 0 ? typeof(int) : typeof(string));
                if (i == 1) writing.Set();
            }
        });
        writer.Start();
        BattleBenchmarkReport report;
        try
        {
            Assert.True(writing.Wait(TimeSpan.FromSeconds(10)));
            report = recorder.BuildReport(Stopwatch.GetTimestamp(), "stopped", default, BattleBenchmarkMissionState.None);
        }
        finally
        {
            stop.Cancel();
            writer.Join();
        }

        Assert.NotEqual(0L, report.Traffic.Sent.MovementDatagrams);
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
        // TryStart made the run folder; Dispose leaves it without a report.
        Assert.Empty(Directory.GetFiles(Path.Combine(outputDirectory, "disposed")));
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

    private sealed class ReceivedMessage : IMessage
    {
    }

    private readonly struct ReceivedPacket : IPacket
    {
        public ReceivedPacket(DeliveryMethod deliveryMethod)
        {
            DeliveryMethod = deliveryMethod;
        }

        public PacketType PacketType => PacketType.Test;
        public DeliveryMethod DeliveryMethod { get; }
    }
}
