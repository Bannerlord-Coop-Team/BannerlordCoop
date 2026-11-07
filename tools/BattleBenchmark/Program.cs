using Common.BattleBenchmark;

// dotnet run --project tools/BattleBenchmark -- compare <baseline-dir> <candidate-dir> [--all] [--filter <regex>] [--out <file>]
// dotnet run --project tools/BattleBenchmark -- summary <runs-dir> [--all] [--filter <regex>] [--out <file>]
// Each directory holds one subdirectory per run, with one JSON report per process. See doc/automated-testing/battle-benchmark.md.

const string Usage =
    "usage: compare <baseline-dir> <candidate-dir> [--all] [--filter <regex>] [--out <file>]\n" +
    "       summary <runs-dir> [--all] [--filter <regex>] [--out <file>]";

var positional = new List<string>();
bool includeAll = false;
string filter = null;
string outputPath = null;
for (int index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--all":
            includeAll = true;
            break;
        case "--filter" when index + 1 < args.Length:
            filter = args[++index];
            break;
        case "--out" when index + 1 < args.Length:
            outputPath = args[++index];
            break;
        default:
            positional.Add(args[index]);
            break;
    }
}

// The tool has no container; it builds the comparison itself.
IBattleBenchmarkComparison comparison = new BattleBenchmarkComparison();
string output;
switch (positional.FirstOrDefault())
{
    case "compare" when positional.Count == 3:
    {
        var baseline = comparison.LoadRuns(positional[1]);
        var candidate = comparison.LoadRuns(positional[2]);
        if (baseline.Count == 0 || candidate.Count == 0)
        {
            Console.Error.WriteLine($"No benchmark runs found: baseline={baseline.Count}, candidate={candidate.Count}");
            return 3;
        }
        output = comparison.Render(comparison.Compare(baseline, candidate, includeAll, filter), baseline.Count, candidate.Count);
        break;
    }
    case "summary" when positional.Count == 2:
    {
        var runs = comparison.LoadRuns(positional[1]);
        if (runs.Count == 0)
        {
            Console.Error.WriteLine($"No benchmark runs found in {positional[1]}");
            return 3;
        }
        output = comparison.RenderSummary(runs, includeAll, filter);
        break;
    }
    default:
        Console.Error.WriteLine(Usage);
        return 2;
}

Console.Write(output);
if (outputPath != null) File.WriteAllText(outputPath, output);
return 0;
