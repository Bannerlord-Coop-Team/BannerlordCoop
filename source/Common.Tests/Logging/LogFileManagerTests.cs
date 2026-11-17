using Common.Logging;
using System.IO;
using Xunit;

namespace Common.Tests.Logging;

public class LogFileManagerTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "LogFileManagerTests_" + Guid.NewGuid().ToString("N"));

    private readonly LogFileManager manager;

    private string LogPath => Path.Combine(tempRoot, "Coop_client.log");
    private string ProcessLogPath => Path.Combine(tempRoot, "Coop_client_1234.log");

    public LogFileManagerTests()
    {
        Directory.CreateDirectory(tempRoot);
        manager = new LogFileManager(tempRoot, "client", 1234);
    }

    [Fact]
    public void Open_LogHeldByAnotherProgram_OpensAClearedProcessLog()
    {
        File.WriteAllText(ProcessLogPath, "earlier process with the same id");

        var logFile = manager.Open(path =>
        {
            if (path == LogPath) throw new IOException("held by another program");
        });

        Assert.Equal(ProcessLogPath, logFile.OpenedPath);
        Assert.False(File.Exists(ProcessLogPath));
    }

    [Fact]
    public void Open_NoPathOpens_OpensNoPath()
    {
        Assert.Null(manager.Open(_ => throw new IOException("held by another program")).OpenedPath);
    }

    [Fact]
    public void Open_CanonicalLogClaimedByALiveInstance_LeavesItsLogAlone()
    {
        manager.Open(_ => { });
        File.WriteAllText(LogPath, "live session");
        using var secondInstance = new LogFileManager(tempRoot, "client", 5678);

        var logFile = secondInstance.Open(_ => { });

        Assert.Equal(Path.Combine(tempRoot, "Coop_client_5678.log"), logFile.OpenedPath);
        Assert.Equal("live session", File.ReadAllText(LogPath));
        Assert.False(File.Exists(Path.Combine(tempRoot, "Coop_client.prev.log")));
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
            Assert.Equal(LogPath, manager.Open(_ => { }).OpenedPath);
        }

        Assert.Equal("last session", File.ReadAllText(previousLogPath));
    }

    public void Dispose()
    {
        manager.Dispose();
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
