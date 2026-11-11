using Common.Logging;
using System.IO;
using Xunit;

namespace Common.Tests.Logging;

public class LogFileOpenerTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "LogFileOpenerTests_" + Guid.NewGuid().ToString("N"));

    private readonly LogFileOpener opener;

    private string LogPath => Path.Combine(tempRoot, "Coop_client.log");
    private string ProcessLogPath => Path.Combine(tempRoot, "Coop_client_1234.log");

    public LogFileOpenerTests()
    {
        Directory.CreateDirectory(tempRoot);
        opener = new LogFileOpener(tempRoot, "client", 1234);
    }

    [Fact]
    public void Open_LogHeldByAnotherProgram_OpensAClearedProcessLog()
    {
        File.WriteAllText(ProcessLogPath, "earlier process with the same id");

        var openedPath = opener.Open(path =>
        {
            if (path == LogPath) throw new IOException("held by another program");
        });

        Assert.Equal(ProcessLogPath, openedPath);
        Assert.False(File.Exists(ProcessLogPath));
    }

    [Fact]
    public void Open_NoPathOpens_ReturnsNull()
    {
        Assert.Null(opener.Open(_ => throw new IOException("held by another program")));
    }

    [Fact]
    public void Open_LogOpenByADeleteSharingReader_ReplacesAnOlderPrevLog()
    {
        var previousLogPath = Path.Combine(tempRoot, "Coop_client.prev.log");
        File.WriteAllText(previousLogPath, "older session");
        File.WriteAllText(LogPath, "last session");

        // A reader like tail -f or the crash reporter's copy, which allows delete.
        using (new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.Equal(LogPath, opener.Open(_ => { }));
        }

        Assert.Equal("last session", File.ReadAllText(previousLogPath));
    }

    public void Dispose()
    {
        opener.Dispose();
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
