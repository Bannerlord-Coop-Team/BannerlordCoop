using Common.Logging;
using System.IO;
using Xunit;

namespace Common.Tests.Logging;

public class LogFileOpenerTests : IDisposable
{
    private const string Template = "{Message:lj}{NewLine}";

    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "LogFileOpenerTests_" + Guid.NewGuid().ToString("N"));

    private readonly string logPath;
    private readonly string fallbackPath;

    public LogFileOpenerTests()
    {
        Directory.CreateDirectory(tempRoot);
        logPath = Path.Combine(tempRoot, "Coop_client.log");
        fallbackPath = Path.Combine(tempRoot, "Coop_client_1234.log");
    }

    [Fact]
    public void OpenFirst_FirstPathOpens_ReturnsItsSink()
    {
        using var sink = LogFileOpener.OpenFirst(new[] { logPath, fallbackPath }, OpenSink, out var openedPath, out var failure);

        Assert.NotNull(sink);
        Assert.Equal(logPath, openedPath);
        Assert.Null(failure);
        Assert.False(File.Exists(fallbackPath));
    }

    [Fact]
    public void OpenFirst_FirstPathLockedByAnotherProgram_OpensTheNextPath()
    {
        using var otherProgram = new FileStream(logPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        using var sink = LogFileOpener.OpenFirst(new[] { logPath, fallbackPath }, OpenSink, out var openedPath, out var failure);

        Assert.NotNull(sink);
        Assert.Equal(fallbackPath, openedPath);
        Assert.IsAssignableFrom<IOException>(failure);
    }

    [Fact]
    public void OpenFirst_NoPathOpens_ReturnsNullAndTheLastFailure()
    {
        using var lockOnLog = new FileStream(logPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var lockOnFallback = new FileStream(fallbackPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        var sink = LogFileOpener.OpenFirst(new[] { logPath, fallbackPath }, OpenSink, out var openedPath, out var failure);

        Assert.Null(sink);
        Assert.Null(openedPath);
        Assert.IsAssignableFrom<IOException>(failure);
    }

    [Fact]
    public void OpenFirst_NoAccess_OpensTheNextPath()
    {
        var sink = LogFileOpener.OpenFirst(
            new[] { logPath, fallbackPath },
            path => path == logPath ? throw new UnauthorizedAccessException("read-only") : "opened " + path,
            out var openedPath,
            out var failure);

        Assert.Equal("opened " + fallbackPath, sink);
        Assert.Equal(fallbackPath, openedPath);
        Assert.IsType<UnauthorizedAccessException>(failure);
    }

    [Fact]
    public void OpenFirst_FailureThatIsNotAFileProblem_Propagates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LogFileOpener.OpenFirst<string>(
            new[] { logPath, fallbackPath },
            path => throw new ArgumentOutOfRangeException("maxFileSizeBytes"),
            out _,
            out _));
    }

    private static SizeLimitedFileSink OpenSink(string path) =>
        new SizeLimitedFileSink(path, Template, 64 * 1024, 4 * 1024, 32 * 1024);

    public void Dispose()
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
