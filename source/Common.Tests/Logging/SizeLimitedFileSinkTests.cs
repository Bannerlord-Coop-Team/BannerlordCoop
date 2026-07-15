using Common.Logging;
using Serilog;
using Serilog.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace Common.Tests.Logging;

/// <summary>
/// Verifies bounded log retention and truncation evidence.
/// </summary>
public sealed class SizeLimitedFileSinkTests : IDisposable
{
    private const string OutputTemplate = "{Message:lj}{NewLine}";
    private const int MaximumBytes = 4096;
    private const int PreservedBeginningBytes = 512;
    private const int CompactionTargetBytes = 3072;

    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly string filePath;

    public SizeLimitedFileSinkTests()
    {
        Directory.CreateDirectory(directory);
        filePath = Path.Combine(directory, "test.log");
    }

    [Fact]
    public void WritesAllEventsWhenBelowLimit()
    {
        using (var logger = CreateLogger())
        {
            logger.Information("first-event");
            logger.Information("second-event");
        }

        var content = File.ReadAllText(filePath);
        Assert.Contains("first-event", content);
        Assert.Contains("second-event", content);
        Assert.DoesNotContain("[LOG TRUNCATED ", content);
    }

    [Fact]
    public void TruncatesMiddleAndPreservesBeginningAndNewestEvents()
    {
        using (var logger = CreateLogger())
        {
            logger.Information("startup-evidence");
            WriteLargeEvents(logger, 18);
            logger.Information("newest-evidence");
        }

        var content = File.ReadAllText(filePath);
        Assert.True(new FileInfo(filePath).Length <= MaximumBytes);
        Assert.Contains("startup-evidence", content);
        Assert.Contains("newest-evidence", content);
        Assert.DoesNotContain("middle-00-", content);
        Assert.Contains("[LOG TRUNCATED ", content);
    }

    [Fact]
    public void RepeatedCompactionsUpdateOneCumulativeMarker()
    {
        using (var logger = CreateLogger())
        {
            logger.Information("startup-evidence");
            WriteLargeEvents(logger, 40);
            logger.Information("newest-evidence");
        }

        var content = File.ReadAllText(filePath);
        Assert.Single(Regex.Matches(content, "\\[LOG TRUNCATED ").Cast<Match>());

        var marker = Regex.Match(content, "Middle truncation count: (\\d+); removed (\\d+) bytes cumulatively");
        Assert.True(marker.Success);
        Assert.True(long.Parse(marker.Groups[1].Value) > 1);
        Assert.True(long.Parse(marker.Groups[2].Value) > 0);
    }

    [Fact]
    public void CompactionKeepsUtf8ContentValid()
    {
        using (var logger = CreateLogger())
        {
            logger.Information("startup-界🙂");
            for (var index = 0; index < 24; index++)
                logger.Information("middle-{Index:D2}-{Payload}", index, string.Concat(Enumerable.Repeat("界🙂", 120)));
            logger.Information("newest-界🙂");
        }

        var bytes = File.ReadAllBytes(filePath);
        var content = new UTF8Encoding(false, true).GetString(bytes);
        Assert.Contains("startup-界🙂", content);
        Assert.Contains("newest-界🙂", content);
    }

    [Fact]
    public void OversizedEventKeepsStartupAndNewestEventSuffix()
    {
        using (var logger = CreateLogger())
        {
            logger.Information("startup-evidence");
            logger.Information("{Payload}newest-tail", string.Concat(Enumerable.Repeat("界🙂", 3000)));
        }

        var bytes = File.ReadAllBytes(filePath);
        var content = new UTF8Encoding(false, true).GetString(bytes);
        Assert.True(bytes.Length <= MaximumBytes);
        Assert.Contains("startup-evidence", content);
        Assert.Contains("newest-tail", content);
        Assert.Contains("Middle truncation count: 1", content);
    }

    [Fact]
    public void ConcurrentWritesRemainComplete()
    {
        const int eventCount = 200;
        using (var logger = CreateLogger(262144, 4096, 200000))
        {
            Parallel.For(0, eventCount, index => logger.Information("concurrent-{Index:D3}", index));
        }

        var lines = File.ReadAllLines(filePath);
        Assert.Equal(eventCount, lines.Length);
        Assert.Equal(eventCount, lines.Distinct().Count());
    }

    private Logger CreateLogger(
        long maximumBytes = MaximumBytes,
        long preservedBeginningBytes = PreservedBeginningBytes,
        long compactionTargetBytes = CompactionTargetBytes)
    {
        var sink = new SizeLimitedFileSink(
            filePath,
            OutputTemplate,
            maximumBytes,
            preservedBeginningBytes,
            compactionTargetBytes);
        return new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
    }

    private static void WriteLargeEvents(ILogger logger, int count)
    {
        for (var index = 0; index < count; index++)
            logger.Information("middle-{Index:D2}-{Payload}", index, new string('x', 520));
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }
}
