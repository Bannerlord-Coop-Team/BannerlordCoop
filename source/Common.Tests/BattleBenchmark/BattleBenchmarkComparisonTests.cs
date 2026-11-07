using Common.BattleBenchmark;
using Common.Serialization;
using System.Diagnostics;
using System.Text.Json;

namespace Common.Tests.BattleBenchmark;

/// <summary>Verifies how benchmark runs are loaded, lined up by process role and compared.</summary>
public class BattleBenchmarkComparisonTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "battle-benchmark-compare-" + Guid.NewGuid().ToString("N"));
    private readonly BattleBenchmarkComparison comparison = new BattleBenchmarkComparison();

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Theory]
    [InlineData(new[] { 10d, 11d, 12d }, new[] { 8d, 8.5d, 9d }, "separated")]
    [InlineData(new[] { 10d, 11d, 12d }, new[] { 9d, 10.5d, 11d }, "within noise")]
    [InlineData(new[] { 10d, 11d }, new[] { 5d, 6d, 7d }, "too few runs")]
    public void Compare_CallsAChangeSeparatedOnlyWhenTheRunRangesDoNotOverlap(double[] baseline, double[] candidate, string verdict)
    {
        BattleBenchmarkMetricComparison row = Assert.Single(comparison.Compare(
            Runs(baseline), Runs(candidate), includeAll: false, filter: "P95Ms"));

        Assert.Equal(verdict, row.Verdict);
        Assert.Equal(candidate.Average() - baseline.Average(), row.Delta, 9);
        Assert.Equal(100d * (candidate.Average() - baseline.Average()) / baseline.Average(), row.DeltaPercent, 9);
    }

    [Fact]
    public void Spread_UsesTheSampleStandardDeviation()
    {
        BattleBenchmarkSpread spread = BattleBenchmarkSpread.Of(new[] { 2d, 4d, 4d, 4d, 5d, 5d, 7d, 9d });

        Assert.Equal(5d, spread.Mean);
        Assert.Equal(Math.Sqrt(32d / 7d), spread.StandardDeviation, 9);
        Assert.Equal(2d, spread.Minimum);
        Assert.Equal(9d, spread.Maximum);
        Assert.Equal(0, BattleBenchmarkSpread.Of(Array.Empty<double>()).Count);
        Assert.Equal(0d, BattleBenchmarkSpread.Of(new[] { 3d }).StandardDeviation);
    }

    [Fact]
    public void ParseRun_KeysRoutesByNameAndTellsSharedRolesApartByController()
    {
        BattleBenchmarkRun run = comparison.ParseRun("run", new[]
        {
            Report("server", "Server", p95: 5, routes: "[]"),
            Report("client", "PlayerTwo", p95: 6, routes: "[{\"Route\":\"PlayerOne\",\"Sent\":{\"MovementBytesPerSecond\":10}}]"),
            Report("client", "PlayerThree", p95: 7, routes: "[]"),
        });

        Assert.Equal(new[] { "client:PlayerThree", "client:PlayerTwo", "server" }, run.Processes.Keys.OrderBy(key => key));
        Assert.Equal(10d, run.Processes["client:PlayerTwo"]["Routes[PlayerOne].Sent.MovementBytesPerSecond"]);
        Assert.Equal(5d, run.Processes["server"]["Frames.P95Ms"]);
        Assert.False(run.Processes["server"].ContainsKey("Process.ProcessId"));
    }

    [Fact]
    public void LoadRuns_ReadsOneRunPerFolderAndSkipsFilesThatAreNotReports()
    {
        WriteRun("A", "run-01", Report("battle-host", "PlayerOne", p95: 20, routes: "[]"));
        WriteRun("A", "run-03", Report("battle-host", "PlayerOne", p95: 22, routes: "[]"));
        Directory.CreateDirectory(Path.Combine(root, "A", "run-05"));
        File.WriteAllText(Path.Combine(root, "A", "run-05", "context.json"), "{\"AvailableMBytes\":1234}");

        IReadOnlyList<BattleBenchmarkRun> runs = comparison.LoadRuns(Path.Combine(root, "A"));

        Assert.Equal(new[] { "run-01", "run-03" }, runs.Select(run => run.Name));
    }

    [Fact]
    public void RecorderReports_RoundTripIntoTheComparison()
    {
        var recorder = new BattleBenchmarkRecorder(
            new FixedSampler(), new FixedProbe(), new JsonFileIO(), Stopwatch.GetTimestamp, _ => { }, _ => { });
        Assert.True(recorder.TryStart("round-trip", "field", TimeSpan.FromSeconds(30), Path.Combine(root, "A", "run-01"), out _));
        recorder.RecordSent("PlayerTwo", reliable: false, 800, 1020, relayed: false);
        Assert.True(recorder.TryStop(out _, out _));

        BattleBenchmarkRun run = Assert.Single(comparison.LoadRuns(Path.Combine(root, "A")));
        IReadOnlyDictionary<string, double> metrics = Assert.Single(run.Processes).Value;

        Assert.Equal(800d, metrics["Routes[PlayerTwo].Sent.MovementBytes"]);
        Assert.True(metrics.ContainsKey("Sections.MovementSend.MsPerSecond"));
        Assert.True(metrics.ContainsKey("Garbage.AllocatedBytesPerSecond"));
    }

    [Fact]
    public void Render_WritesOneMarkdownRowPerMetric()
    {
        IReadOnlyList<BattleBenchmarkMetricComparison> rows = comparison.Compare(
            Runs(new[] { 10d, 11d, 12d }), Runs(new[] { 8d, 8.5d, 9d }), includeAll: false, filter: "P95Ms");

        string table = comparison.Render(rows, 3, 3);

        Assert.Contains("### battle-host", table);
        Assert.Contains("| Frames.P95Ms | 11.0 ± 1.00 [10.0, 12.0] (n=3) | 8.50 ± 0.50 [8.00, 9.00] (n=3) | -2.50 | -22.7% | separated |", table);
    }

    private IReadOnlyList<BattleBenchmarkRun> Runs(double[] p95) =>
        p95.Select((value, index) => comparison.ParseRun($"run-{index}", new[] { Report("battle-host", "PlayerOne", value, "[]") }))
            .ToList();

    private void WriteRun(string side, string run, string json)
    {
        string directory = Path.Combine(root, side, run, "nested");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "report.json"), json);
    }

    private static string Report(string role, string controllerId, double p95, string routes)
    {
        using JsonDocument routeList = JsonDocument.Parse(routes);
        return JsonSerializer.Serialize(new
        {
            Schema = BattleBenchmarkReport.CurrentSchema,
            Process = new { Role = role, ControllerId = controllerId, ProcessId = 42 },
            Frames = new { P95Ms = p95 },
            Routes = routeList.RootElement,
        });
    }

    private sealed class FixedSampler : IBattleBenchmarkProcessSampler
    {
        public void EnableAllocationMonitoring()
        {
        }

        public BattleBenchmarkProcessSample Sample() => default;
    }

    private sealed class FixedProbe : IBattleBenchmarkMissionProbe
    {
        public BattleBenchmarkMissionState Capture() => BattleBenchmarkMissionState.None;
    }
}
