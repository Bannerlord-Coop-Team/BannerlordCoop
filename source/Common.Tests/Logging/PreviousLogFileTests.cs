using Common.Logging;
using System.IO;
using Xunit;

namespace Common.Tests.Logging;

public class PreviousLogFileTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "PreviousLogFileTests_" + Guid.NewGuid().ToString("N"));

    private readonly string logPath;
    private readonly string previousLogPath;

    public PreviousLogFileTests()
    {
        Directory.CreateDirectory(tempRoot);
        logPath = Path.Combine(tempRoot, "Coop_client.log");
        previousLogPath = Path.Combine(tempRoot, "Coop_client.prev.log");
    }

    [Fact]
    public void GetPath_InsertsPrevBeforeTheExtension()
    {
        Assert.Equal(previousLogPath, PreviousLogFile.GetPath(logPath));
    }

    [Fact]
    public void TryKeep_MovesTheLogToThePrevLog()
    {
        File.WriteAllText(logPath, "last session");

        Assert.True(PreviousLogFile.TryKeep(logPath, out var failure));

        Assert.Null(failure);
        Assert.False(File.Exists(logPath));
        Assert.Equal("last session", File.ReadAllText(previousLogPath));
    }

    [Fact]
    public void TryKeep_ReplacesAnOlderPrevLog()
    {
        File.WriteAllText(previousLogPath, "older session");
        File.WriteAllText(logPath, "last session");

        Assert.True(PreviousLogFile.TryKeep(logPath, out var failure));

        Assert.Null(failure);
        Assert.False(File.Exists(logPath));
        Assert.Equal("last session", File.ReadAllText(previousLogPath));
    }

    [Fact]
    public void TryKeep_LogOpenByADeleteSharingReader_ReplacesAnOlderPrevLog()
    {
        File.WriteAllText(previousLogPath, "older session");
        File.WriteAllText(logPath, "last session");

        // A reader like tail -f or the crash reporter's copy, which allows delete.
        using (new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.True(PreviousLogFile.TryKeep(logPath, out var failure));
            Assert.Null(failure);
        }

        Assert.False(File.Exists(logPath));
        Assert.Equal("last session", File.ReadAllText(previousLogPath));
    }

    [Fact]
    public void TryKeep_MissingLog_LeavesThePrevLogAlone()
    {
        File.WriteAllText(previousLogPath, "older session");

        Assert.True(PreviousLogFile.TryKeep(logPath, out var failure));

        Assert.Null(failure);
        Assert.Equal("older session", File.ReadAllText(previousLogPath));
    }

    [Fact]
    public void TryKeep_LockedLog_ReportsTheFailureWithoutThrowing()
    {
        File.WriteAllText(previousLogPath, "older session");
        File.WriteAllText(logPath, "last session");

        using (new FileStream(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(PreviousLogFile.TryKeep(logPath, out var failure));
            Assert.NotNull(failure);
        }

        Assert.Equal("last session", File.ReadAllText(logPath));
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
