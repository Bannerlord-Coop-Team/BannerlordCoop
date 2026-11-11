using Common.Logging;
using System.IO;
using Xunit;

namespace Common.Tests.Logging;

public class LogFileOpenerTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "LogFileOpenerTests_" + Guid.NewGuid().ToString("N"));

    public LogFileOpenerTests()
    {
        Directory.CreateDirectory(tempRoot);
    }

    [Fact]
    public void OpenFirst_FirstPathLockedByAnotherProgram_OpensTheNextPath()
    {
        var logPath = Path.Combine(tempRoot, "Coop_client.log");
        var fallbackPath = Path.Combine(tempRoot, "Coop_client_1234.log");
        using var otherProgram = new FileStream(logPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        using var sink = new LogFileOpener().OpenFirst(
            new[] { logPath, fallbackPath },
            path => new SizeLimitedFileSink(path, "{Message:lj}{NewLine}", 64 * 1024, 4 * 1024, 32 * 1024),
            out var openedPath,
            out var failure);

        Assert.NotNull(sink);
        Assert.Equal(fallbackPath, openedPath);
        Assert.IsAssignableFrom<IOException>(failure);
    }

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
