using Common.BattleBenchmark;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BattleBenchmark;

/// <summary>
/// Loads battle benchmark runs and compares a baseline set with a candidate set: every metric's mean, run-to-run spread
/// and delta, per process role.
/// </summary>
public interface IBattleBenchmarkComparison
{
    IReadOnlyList<BattleBenchmarkRun> LoadRuns(string root);

    BattleBenchmarkRun ParseRun(string name, IEnumerable<string> reportJson);

    IReadOnlyList<BattleBenchmarkMetricComparison> Compare(
        IReadOnlyList<BattleBenchmarkRun> baseline,
        IReadOnlyList<BattleBenchmarkRun> candidate,
        bool includeAll,
        string filter = null);

    string Render(IReadOnlyList<BattleBenchmarkMetricComparison> comparisons, int baselineRuns, int candidateRuns);

    string RenderMessageTypes(IReadOnlyList<BattleBenchmarkRun> baseline, IReadOnlyList<BattleBenchmarkRun> candidate, string filter = null);

    string RenderSummary(IReadOnlyList<BattleBenchmarkRun> runs, bool includeAll, string filter = null);
}

/// <inheritdoc cref="IBattleBenchmarkComparison"/>
public class BattleBenchmarkComparison : IBattleBenchmarkComparison
{
    // Metrics shown without --all: the ones a battle performance change is judged on.
    private static readonly Regex HeadlinePattern = new Regex(
        @"^(Frames\.(FramesPerSecond|AverageMs|P50Ms|P95Ms|P99Ms|MaxMs)" +
        @"|Sections\.\w+\.MsPerSecond" +
        @"|Garbage\.(Gen0Collections|Gen1Collections|Gen2Collections|AllocatedBytesPerSecond)" +
        @"|Cpu\.PercentOfOneCore" +
        @"|Memory\.PeakWorkingSetMb" +
        @"|MissionEnd\.(ActiveAgents|OwnedAgents)" +
        @"|Traffic\.(Sent|Received)\.\w+PerSecond" +
        @"|ReceivedMessages\.PerSecond" +
        @"|MessageTraffic\.(Sent|Received)\.Total\.(CountPerSecond|BytesPerSecond))$",
        RegexOptions.Compiled);

    // One message or packet type's rates, which RenderMessageTypes tables instead of the main tables.
    private static readonly Regex MessageTypePattern = new Regex(
        @"^MessageTraffic\.(?<direction>Sent|Received)\.ByType\.(?<type>.+)\.(?<metric>BytesPerSecond|CountPerSecond|AverageBytes)$",
        RegexOptions.Compiled);

    // A type's counts and rates; a run with none of the type has no entry, so it counts as 0. Its average size has no value then.
    private static readonly Regex ZeroWhenMissingPattern = new Regex(
        @"^MessageTraffic\.(?<direction>Sent|Received)\.ByType\..+\.(Count|Bytes|CountPerSecond|BytesPerSecond)$",
        RegexOptions.Compiled);

    // Identity and bookkeeping fields, not measurements.
    private static readonly HashSet<string> IgnoredPaths = new HashSet<string>(StringComparer.Ordinal)
    {
        "Process.ProcessId",
        "Process.ProcessorCount",
        "Window.RequestedSeconds",
    };

    /// <summary>Loads every run below <paramref name="root"/>: each subdirectory is one run, holding one JSON report per process.</summary>
    public IReadOnlyList<BattleBenchmarkRun> LoadRuns(string root)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"No run directory at {root}");

        var runs = new List<BattleBenchmarkRun>();
        foreach (string runDirectory in Directory.GetDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
        {
            var reports = Directory.GetFiles(runDirectory, "*.json", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => (Path: path, Json: File.ReadAllText(path)))
                .Where(file => IsReport(file.Json))
                .ToList();
            if (reports.Count == 0) continue;

            runs.Add(ParseRun(Path.GetFileName(runDirectory), reports.Select(file => file.Json)));
        }
        return runs;
    }

    /// <summary>Builds one run from its processes' JSON reports.</summary>
    public BattleBenchmarkRun ParseRun(string name, IEnumerable<string> reportJson)
    {
        var processes = new List<(string Role, string ControllerId, Dictionary<string, double> Metrics)>();
        foreach (string json in reportJson)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            JsonElement process = root.GetProperty(nameof(BattleBenchmarkReport.Process));
            string role = process.GetProperty(nameof(BattleBenchmarkProcessInfo.Role)).GetString();
            string controllerId = process.TryGetProperty(nameof(BattleBenchmarkProcessInfo.ControllerId), out JsonElement id) &&
                                  id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
            var metrics = new Dictionary<string, double>(StringComparer.Ordinal);
            Flatten(root, string.Empty, metrics);
            processes.Add((role, controllerId, metrics));
        }

        // Plain roles line runs up; only a role held by several processes needs the controller id to tell them apart.
        var byKey = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);
        foreach (var group in processes.GroupBy(process => process.Role))
        {
            bool shared = group.Count() > 1;
            foreach (var process in group)
            {
                string key = shared ? $"{process.Role}:{process.ControllerId}" : process.Role;
                // A rerun into the same folder leaves the old process's report next to the new one.
                if (byKey.ContainsKey(key))
                    throw new InvalidDataException($"Run {name} has more than one report for {key}; remove the stale one.");
                byKey[key] = process.Metrics;
            }
        }
        return new BattleBenchmarkRun(name, byKey);
    }

    /// <summary>Compares every metric present in either set, per process key.</summary>
    public IReadOnlyList<BattleBenchmarkMetricComparison> Compare(
        IReadOnlyList<BattleBenchmarkRun> baseline,
        IReadOnlyList<BattleBenchmarkRun> candidate,
        bool includeAll,
        string filter = null)
    {
        Regex filterPattern = string.IsNullOrEmpty(filter) ? null : new Regex(filter, RegexOptions.IgnoreCase);
        var results = new List<BattleBenchmarkMetricComparison>();
        foreach (string processKey in baseline.Concat(candidate)
                     .SelectMany(run => run.Processes.Keys)
                     .Distinct()
                     .OrderBy(ProcessOrder)
                     .ThenBy(key => key, StringComparer.Ordinal))
        {
            IEnumerable<string> metricNames = baseline.Concat(candidate)
                .Where(run => run.Processes.ContainsKey(processKey))
                .SelectMany(run => run.Processes[processKey].Keys)
                .Distinct()
                .Where(metric => includeAll || HeadlinePattern.IsMatch(metric))
                .Where(metric => filterPattern == null || filterPattern.IsMatch(metric))
                .OrderBy(metric => metric, StringComparer.Ordinal);

            foreach (string metric in metricNames)
            {
                results.Add(new BattleBenchmarkMetricComparison(
                    processKey,
                    metric,
                    BattleBenchmarkSpread.Of(Values(baseline, processKey, metric)),
                    BattleBenchmarkSpread.Of(Values(candidate, processKey, metric))));
            }
        }
        return results;
    }

    /// <summary>Renders comparisons as one Markdown table per process key.</summary>
    public string Render(IReadOnlyList<BattleBenchmarkMetricComparison> comparisons, int baselineRuns, int candidateRuns)
    {
        var text = new StringBuilder();
        text.AppendLine($"Baseline runs: {baselineRuns}, candidate runs: {candidateRuns}. " +
                        "Spread is the sample standard deviation and [min, max] across runs. " +
                        "\"separated\" means every candidate run is on the same side of every baseline run. " +
                        "With 3 runs a side any one metric separates by chance one time in ten, so judge a change by the metric it targets.");
        foreach (var group in comparisons.GroupBy(comparison => comparison.ProcessKey))
        {
            text.AppendLine();
            text.AppendLine($"### {group.Key}");
            text.AppendLine();
            text.AppendLine("| Metric | Baseline | Candidate | Delta | Delta % | Verdict |");
            text.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (BattleBenchmarkMetricComparison comparison in group)
            {
                text.AppendLine($"| {comparison.Metric} | {Format(comparison.Baseline)} | {Format(comparison.Candidate)} | " +
                                $"{FormatNumber(comparison.Delta)} | {FormatPercent(comparison.DeltaPercent)} | {comparison.Verdict} |");
            }
        }
        return text.ToString();
    }

    /// <summary>
    /// Renders bytes per second per message or packet type, per process key and direction, largest change first. A type
    /// missing from a report that has the per-type counts had none in that run, so it counts as 0 there. Types with no
    /// bytes in any run are left out, and so are types whose bytes metric doesn't match <paramref name="filter"/>.
    /// </summary>
    public string RenderMessageTypes(IReadOnlyList<BattleBenchmarkRun> baseline, IReadOnlyList<BattleBenchmarkRun> candidate, string filter = null)
    {
        Regex filterPattern = string.IsNullOrEmpty(filter) ? null : new Regex(filter, RegexOptions.IgnoreCase);
        var text = new StringBuilder();
        text.AppendLine();
        text.AppendLine("## Bytes per message type");
        text.AppendLine();
        text.AppendLine("Serialized bytes per second handed to the connection or taken off it, by message or packet type, largest " +
                        "change first. Msgs/s and bytes/msg are means over the runs.");
        foreach (string processKey in baseline.Concat(candidate)
                     .SelectMany(run => run.Processes.Keys)
                     .Distinct()
                     .OrderBy(ProcessOrder)
                     .ThenBy(key => key, StringComparer.Ordinal))
        {
            foreach (string direction in new[] { "Sent", "Received" })
            {
                // Reports written before the per-type counts existed have no total, so they are left out.
                string total = $"MessageTraffic.{direction}.Total.BytesPerSecond";
                List<IReadOnlyDictionary<string, double>> baselineRuns = MetricsOf(baseline, processKey, total);
                List<IReadOnlyDictionary<string, double>> candidateRuns = MetricsOf(candidate, processKey, total);

                BattleBenchmarkMetricComparison Rate(string type, string rate)
                {
                    string metric = $"MessageTraffic.{direction}.ByType.{type}.{rate}";
                    return new BattleBenchmarkMetricComparison(
                        processKey,
                        metric,
                        BattleBenchmarkSpread.Of(Values(baseline, processKey, metric)),
                        BattleBenchmarkSpread.Of(Values(candidate, processKey, metric)));
                }

                var rows = baselineRuns.Concat(candidateRuns)
                    .SelectMany(metrics => metrics.Keys)
                    .Select(metric => MessageTypePattern.Match(metric))
                    .Where(match => match.Success && match.Groups["direction"].Value == direction)
                    .Select(match => match.Groups["type"].Value)
                    .Distinct()
                    .Select(type => (Type: type, Bytes: Rate(type, "BytesPerSecond"), Count: Rate(type, "CountPerSecond")))
                    .Where(row => row.Bytes.Baseline.Maximum > 0 || row.Bytes.Candidate.Maximum > 0)
                    .Where(row => filterPattern == null || filterPattern.IsMatch(row.Bytes.Metric))
                    // Rounded so float residue on an unchanged type doesn't decide the order; the busiest type goes first then.
                    .OrderByDescending(row => double.IsNaN(row.Bytes.Delta) ? double.MaxValue : Math.Round(Math.Abs(row.Bytes.Delta), 3))
                    .ThenByDescending(row => Math.Max(row.Bytes.Baseline.Mean, row.Bytes.Candidate.Mean))
                    .ThenBy(row => row.Type, StringComparer.Ordinal)
                    .ToList();
                if (rows.Count == 0) continue;

                text.AppendLine();
                text.AppendLine($"### {processKey} {direction.ToLowerInvariant()}");
                text.AppendLine();
                text.AppendLine("| Type | Bytes/s baseline | Bytes/s candidate | Delta | Delta % | Verdict | " +
                                "Msgs/s baseline | Msgs/s candidate | Bytes/msg baseline | Bytes/msg candidate |");
                text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
                foreach (var row in rows)
                {
                    text.AppendLine($"| {row.Type} | {Format(row.Bytes.Baseline)} | {Format(row.Bytes.Candidate)} | " +
                                    $"{FormatNumber(row.Bytes.Delta)} | {FormatPercent(row.Bytes.DeltaPercent)} | {row.Bytes.Verdict} | " +
                                    $"{FormatNumber(row.Count.Baseline.Mean)} | {FormatNumber(row.Count.Candidate.Mean)} | " +
                                    $"{FormatNumber(BytesPerMessage(row.Bytes.Baseline, row.Count.Baseline))} | " +
                                    $"{FormatNumber(BytesPerMessage(row.Bytes.Candidate, row.Count.Candidate))} |");
                }
            }
        }
        return text.ToString();
    }

    private static List<IReadOnlyDictionary<string, double>> MetricsOf(
        IEnumerable<BattleBenchmarkRun> runs,
        string processKey,
        string requiredMetric)
    {
        var result = new List<IReadOnlyDictionary<string, double>>();
        foreach (BattleBenchmarkRun run in runs)
        {
            if (run.Processes.TryGetValue(processKey, out var metrics) && metrics.ContainsKey(requiredMetric))
                result.Add(metrics);
        }
        return result;
    }

    // The ratio of the means, so a run with no messages of the type doesn't count as 0 bytes per message.
    private static double BytesPerMessage(BattleBenchmarkSpread bytesPerSecond, BattleBenchmarkSpread countPerSecond) =>
        countPerSecond.Count == 0 || countPerSecond.Mean == 0 ? double.NaN : bytesPerSecond.Mean / countPerSecond.Mean;

    /// <summary>Renders one set's mean and spread per metric, for posting baseline numbers.</summary>
    public string RenderSummary(IReadOnlyList<BattleBenchmarkRun> runs, bool includeAll, string filter = null)
    {
        IReadOnlyList<BattleBenchmarkMetricComparison> rows = Compare(runs, Array.Empty<BattleBenchmarkRun>(), includeAll, filter);
        var text = new StringBuilder();
        text.AppendLine($"Runs: {runs.Count}. Spread is the sample standard deviation, [min, max] and the coefficient of variation.");
        foreach (var group in rows.GroupBy(row => row.ProcessKey))
        {
            text.AppendLine();
            text.AppendLine($"### {group.Key}");
            text.AppendLine();
            text.AppendLine("| Metric | Mean ± SD [min, max] | CV % |");
            text.AppendLine("| --- | --- | --- |");
            foreach (BattleBenchmarkMetricComparison row in group)
                text.AppendLine($"| {row.Metric} | {Format(row.Baseline)} | {FormatPercent(row.Baseline.CoefficientOfVariationPercent)} |");
        }
        return text.ToString();
    }

    internal static void Flatten(JsonElement element, string path, IDictionary<string, double> metrics)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                    Flatten(property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}", metrics);
                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    // Route lists are keyed by route name so the same route lines up across runs.
                    string key = item.ValueKind == JsonValueKind.Object &&
                                 item.TryGetProperty(nameof(BattleBenchmarkRouteTraffic.Route), out JsonElement route) &&
                                 route.ValueKind == JsonValueKind.String
                        ? route.GetString()
                        : index.ToString(CultureInfo.InvariantCulture);
                    Flatten(item, $"{path}[{key}]", metrics);
                    index++;
                }
                break;
            case JsonValueKind.Number:
                if (!IgnoredPaths.Contains(path)) metrics[path] = element.GetDouble();
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (!IgnoredPaths.Contains(path)) metrics[path] = element.GetBoolean() ? 1d : 0d;
                break;
        }
    }

    private static bool IsReport(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(nameof(BattleBenchmarkReport.Schema), out JsonElement schema) &&
                   schema.ValueKind == JsonValueKind.String &&
                   schema.GetString() == BattleBenchmarkReport.CurrentSchema;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // Compare and RenderMessageTypes both read through this, so the two tables count the same runs.
    private static IEnumerable<double> Values(IEnumerable<BattleBenchmarkRun> runs, string processKey, string metric)
    {
        Match perType = ZeroWhenMissingPattern.Match(metric);
        // Reports written before the per-type counts existed have no total, so they are left out.
        string total = perType.Success ? $"MessageTraffic.{perType.Groups["direction"].Value}.Total.BytesPerSecond" : null;
        foreach (BattleBenchmarkRun run in runs)
        {
            if (!run.Processes.TryGetValue(processKey, out var metrics)) continue;
            if (metrics.TryGetValue(metric, out double value))
                yield return value;
            else if (total != null && metrics.ContainsKey(total))
                yield return 0d;
        }
    }

    private static int ProcessOrder(string processKey)
    {
        if (processKey == "server") return 0;
        if (processKey.StartsWith("battle-host", StringComparison.Ordinal)) return 1;
        return 2;
    }

    private static string Format(BattleBenchmarkSpread spread)
    {
        if (spread.Count == 0) return "n/a";
        return $"{FormatNumber(spread.Mean)} ± {FormatNumber(spread.StandardDeviation)} " +
               $"[{FormatNumber(spread.Minimum)}, {FormatNumber(spread.Maximum)}] (n={spread.Count})";
    }

    internal static string FormatNumber(double value)
    {
        if (double.IsNaN(value)) return "n/a";
        double magnitude = Math.Abs(value);
        string format = magnitude >= 1000 ? "N0" : magnitude >= 10 ? "0.0" : magnitude >= 0.1 ? "0.00" : "0.####";
        string text = value.ToString(format, CultureInfo.InvariantCulture);
        // Float residue on an unchanged metric, like -1e-13, would print as -0.
        return text == "-0" ? "0" : text;
    }

    private static string FormatPercent(double value) =>
        double.IsNaN(value) ? "n/a" : value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%";
}

/// <summary>One run's metrics, flattened to dotted paths, per process key (<c>server</c>, <c>battle-host</c>, <c>client</c>).</summary>
public sealed class BattleBenchmarkRun
{
    public BattleBenchmarkRun(string name, IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> processes)
    {
        Name = name;
        Processes = processes;
    }

    public string Name { get; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Processes { get; }
}

/// <summary>The mean and run-to-run spread of one metric across a set of runs.</summary>
public readonly struct BattleBenchmarkSpread
{
    public int Count { get; }
    public double Mean { get; }

    /// <summary>The sample standard deviation; 0 for a single run.</summary>
    public double StandardDeviation { get; }
    public double Minimum { get; }
    public double Maximum { get; }

    public double CoefficientOfVariationPercent =>
        Count == 0 || Mean == 0 ? double.NaN : 100d * StandardDeviation / Math.Abs(Mean);

    private BattleBenchmarkSpread(int count, double mean, double standardDeviation, double minimum, double maximum)
    {
        Count = count;
        Mean = mean;
        StandardDeviation = standardDeviation;
        Minimum = minimum;
        Maximum = maximum;
    }

    public static BattleBenchmarkSpread Of(IEnumerable<double> values)
    {
        double[] samples = values.ToArray();
        if (samples.Length == 0) return new BattleBenchmarkSpread(0, double.NaN, double.NaN, double.NaN, double.NaN);

        double mean = samples.Average();
        double variance = samples.Length < 2
            ? 0d
            : samples.Sum(value => (value - mean) * (value - mean)) / (samples.Length - 1);
        return new BattleBenchmarkSpread(samples.Length, mean, Math.Sqrt(variance), samples.Min(), samples.Max());
    }
}

/// <summary>One metric of one process key, baseline against candidate.</summary>
public sealed class BattleBenchmarkMetricComparison
{
    public BattleBenchmarkMetricComparison(
        string processKey,
        string metric,
        BattleBenchmarkSpread baseline,
        BattleBenchmarkSpread candidate)
    {
        ProcessKey = processKey;
        Metric = metric;
        Baseline = baseline;
        Candidate = candidate;
    }

    public string ProcessKey { get; }
    public string Metric { get; }
    public BattleBenchmarkSpread Baseline { get; }
    public BattleBenchmarkSpread Candidate { get; }

    public double Delta => Baseline.Count == 0 || Candidate.Count == 0 ? double.NaN : Candidate.Mean - Baseline.Mean;

    public double DeltaPercent => double.IsNaN(Delta) || Baseline.Mean == 0 ? double.NaN : 100d * Delta / Math.Abs(Baseline.Mean);

    /// <summary>
    /// <c>separated</c> when every candidate run lies above, or every one below, every baseline run; otherwise the change
    /// is within run-to-run noise. Fewer than 3 runs on either side is <c>too few runs</c>.
    /// </summary>
    public string Verdict
    {
        get
        {
            if (Baseline.Count < 3 || Candidate.Count < 3) return "too few runs";
            if (Candidate.Minimum > Baseline.Maximum || Candidate.Maximum < Baseline.Minimum) return "separated";
            return "within noise";
        }
    }
}
