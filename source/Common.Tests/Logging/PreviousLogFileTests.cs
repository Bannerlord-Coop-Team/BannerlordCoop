using Common.Logging;
using System.IO;
using Xunit;

namespace Common.Tests.Logging;

public class PreviousLogFileTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "PreviousLogFileTests_" + Guid.NewGuid().ToString("N"));

    public PreviousLogFileTests()
    {
        Directory.CreateDirectory(tempRoot);
    }

    [Fact]
    public void TryKeep_LogOpenByADeleteSharingReader_ReplacesAnOlderPrevLog()
    {
        var logPath = Path.Combine(tempRoot, "Coop_client.log");
        var previousLogPath = Path.Combine(tempRoot, "Coop_client.prev.log");
        File.WriteAllText(previousLogPath, "older session");
        File.WriteAllText(logPath, "last session");

        // A reader like tail -f or the crash reporter's copy, which allows delete.
        using (new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.True(new PreviousLogFile().TryKeep(logPath, out var failure));
            Assert.Null(failure);
        }

        Assert.False(File.Exists(logPath));
        Assert.Equal("last session", File.ReadAllText(previousLogPath));
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
