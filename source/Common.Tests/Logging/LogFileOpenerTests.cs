using Common.Logging;
using Serilog.Core;
using System.IO;
using Xunit;

namespace Common.Tests.Logging;

public class LogFileOpenerTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "LogFileOpenerTests_" + Guid.NewGuid().ToString("N"));

    private string LogPath => Path.Combine(tempRoot, "Coop_client.log");
    private string ProcessLogPath => Path.Combine(tempRoot, "Coop_client_1234.log");

    public LogFileOpenerTests()
    {
        Directory.CreateDirectory(tempRoot);
    }

    [Fact]
    public void Open_LogLockedByAnotherProgram_OpensAClearedProcessLog()
    {
        File.WriteAllText(ProcessLogPath, "earlier process with the same id");
        using var otherProgram = new FileStream(LogPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var opener = new LogFileOpener(LogPath, ProcessLogPath, canonicalLogClaimed: true);

        using (opener.Open(CreateSink) as IDisposable)
        {
            Assert.Equal(ProcessLogPath, opener.OpenedPath);
            Assert.Equal(0, new FileInfo(ProcessLogPath).Length);
        }
    }

    [Fact]
    public void Open_EveryPathLocked_OpensNothing()
    {
        using var lockedLog = new FileStream(LogPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var lockedProcessLog = new FileStream(ProcessLogPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var opener = new LogFileOpener(LogPath, ProcessLogPath, canonicalLogClaimed: true);

        Assert.Null(opener.Open(CreateSink));
        Assert.Null(opener.OpenedPath);
    }

    [Fact]
    public void Open_LogOpenByADeleteSharingReader_ReplacesAnOlderPrevLog()
    {
        var previousLogPath = Path.Combine(tempRoot, "Coop_client.prev.log");
        File.WriteAllText(previousLogPath, "older session");
        File.WriteAllText(LogPath, "last session");
        var opener = new LogFileOpener(LogPath, ProcessLogPath, canonicalLogClaimed: true);

        // A reader like tail -f or the crash reporter's copy, which allows delete.
        using (new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (opener.Open(CreateSink) as IDisposable)
        {
            Assert.Equal(LogPath, opener.OpenedPath);
        }

        Assert.Equal("last session", File.ReadAllText(previousLogPath));
    }

    private static ILogEventSink CreateSink(string path) =>
        new SizeLimitedFileSink(path, "{Message:lj}{NewLine}", 64 * 1024, 4 * 1024, 32 * 1024);

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
