using System.Diagnostics;
using System.Text;

namespace CoopMcpServer.Tests;

public sealed class ModBuildServiceTests : IDisposable
{
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "BuildFixture-" + Guid.NewGuid().ToString("N"));

    public ModBuildServiceTests() => Directory.CreateDirectory(root);

    [Theory]
    [InlineData("stdout")]
    [InlineData("stderr")]
    public async Task LogOpenFailureDoesNotStartTheChild(string stream)
    {
        if (!OperatingSystem.IsWindows()) return;
        string log = Path.Combine(root, "Coop-" + stream + ".log");
        File.WriteAllText(log, "existing evidence");
        string marker = Path.Combine(root, "started.txt");
        var info = PowerShell($"[IO.File]::WriteAllText('{Quote(marker)}', 'started')");

        await Assert.ThrowsAsync<IOException>(() => new ModBuildService(new WindowsJobBuildProcessRunner(new BuildCleanupRecovery())).RunBuildAsync(info, root, "Coop", default));

        await Task.Delay(1500);
        Assert.False(File.Exists(marker));
        Assert.Equal("existing evidence", File.ReadAllText(log));
    }

    [Fact]
    public async Task CancellationWaitsForTheOwnedChildBeforeReturning()
    {
        if (!OperatingSystem.IsWindows()) return;
        string marker = Path.Combine(root, "pid.txt");
        var info = PowerShell($"[IO.File]::WriteAllText('{Quote(marker)}', $PID.ToString()); Start-Sleep -Seconds 30");
        using var cancellation = new CancellationTokenSource();
        Task build = new ModBuildService(new WindowsJobBuildProcessRunner(new BuildCleanupRecovery())).RunBuildAsync(info, root, "Coop", cancellation.Token);
        Process child = null;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!File.Exists(marker)) await Task.Delay(20, deadline.Token);
            child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(marker, deadline.Token)));
            Assert.False(child.HasExited);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => build.WaitAsync(TimeSpan.FromSeconds(10)));

            Assert.True(child.HasExited);
            using var stdout = new FileStream(Path.Combine(root, "Coop-stdout.log"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var stderr = new FileStream(Path.Combine(root, "Coop-stderr.log"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally
        {
            cancellation.Cancel();
            if (child != null)
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
                child.Dispose();
            }
            try { await build; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task InvalidRedirectionDoesNotStartTheChild()
    {
        if (!OperatingSystem.IsWindows()) return;
        string marker = Path.Combine(root, "escaped.txt");
        var info = PowerShell($"Start-Sleep -Seconds 1; [IO.File]::WriteAllText('{Quote(marker)}', 'escaped')");
        info.RedirectStandardError = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ModBuildService(new WindowsJobBuildProcessRunner(new BuildCleanupRecovery())).RunBuildAsync(info, root, "Coop", default));

        await Task.Delay(3000);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task SuccessfulChildDrainsBothLogs()
    {
        if (!OperatingSystem.IsWindows()) return;
        var info = PowerShell("[Console]::Out.Write('build output'); [Console]::Error.Write('build error')");

        await new ModBuildService(new WindowsJobBuildProcessRunner(new BuildCleanupRecovery())).RunBuildAsync(info, root, "Coop", default);

        Assert.Equal("build output", File.ReadAllText(Path.Combine(root, "Coop-stdout.log")));
        Assert.Equal("build error", File.ReadAllText(Path.Combine(root, "Coop-stderr.log")));
    }

    private ProcessStartInfo PowerShell(string script)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) info.ArgumentList.Add(argument);
        return info;
    }

    private static string Quote(string value) => value.Replace("'", "''");

    public void Dispose() => Directory.Delete(root, recursive: true);
}
