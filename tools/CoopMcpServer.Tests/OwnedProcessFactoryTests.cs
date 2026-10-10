using System.Diagnostics;

namespace CoopMcpServer.Tests;

public sealed class OwnedProcessFactoryTests
{
    private string PowerShell => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");

    private ProcessStartInfo Script(string script)
    {
        var info = new ProcessStartInfo(PowerShell) { UseShellExecute = false, WorkingDirectory = Path.GetTempPath() };
        info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-EncodedCommand");
        info.ArgumentList.Add(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script)));
        return info;
    }

    [Fact]
    public async Task JobOwnsGrandchildrenAfterRootExitAndLeavesUnrelatedProcessAlive()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CoopJobTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string evidence = Path.Combine(directory, "child.txt");
        string grandchild = $"[IO.File]::WriteAllText('{evidence}', [string]$PID); Start-Sleep -Seconds 120";
        string grandchildEncoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(grandchild));
        string child = $"Start-Process '{PowerShell}' -ArgumentList '-NoProfile','-NonInteractive','-EncodedCommand','{grandchildEncoded}' | Out-Null; Start-Sleep -Seconds 120";
        string childEncoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(child));
        using var unrelated = Process.Start(Script("Start-Sleep -Seconds 120"));
        using var owned = new OwnedProcessFactory().Start(Script($"Start-Process '{PowerShell}' -ArgumentList '-NoProfile','-NonInteractive','-EncodedCommand','{childEncoded}' | Out-Null"));
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (!File.Exists(evidence) || owned.IsAlive) await Task.Delay(50, timeout.Token);
            int pid = int.Parse(File.ReadAllText(evidence));
            using var grandchildProcess = Process.GetProcessById(pid);
            Assert.True(owned.IsTreeAlive);
            var elapsed = Stopwatch.StartNew();
            await owned.StopAsync(TimeSpan.Zero);
            Assert.False(owned.IsTreeAlive);
            Assert.False(owned.IsAlive);
            Assert.True(grandchildProcess.HasExited);
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(7));
            Assert.False(unrelated.HasExited);
            await owned.StopAsync(TimeSpan.Zero);
        }
        finally
        {
            await owned.StopAsync(TimeSpan.Zero);
            if (!unrelated.HasExited) unrelated.Kill();
            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await unrelated.WaitForExitAsync(cleanupTimeout.Token);
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task DisposingOwnershipHandleKillsRemainingOwnedProcesses()
    {
        var owned = new OwnedProcessFactory().Start(Script("Start-Sleep -Seconds 120"));
        using var observed = Process.GetProcessById(owned.Pid);
        Assert.Equal(observed.StartTime.ToUniversalTime(), owned.StartedUtc);
        owned.Dispose();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(7));
        await observed.WaitForExitAsync(timeout.Token);
    }

    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("with spaces", "\"with spaces\"")]
    [InlineData("trailing\\", "\"trailing\\\\\"")]
    [InlineData("quote\"", "\"quote\\\"\"")]
    public void NativeCommandLinePreservesArgumentBoundaries(string value, string expected) =>
        Assert.Equal(expected, OwnedProcessFactory.QuoteArgument(value));
}
