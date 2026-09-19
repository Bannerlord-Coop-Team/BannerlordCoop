using System.Diagnostics;

namespace CoopMcpServer.Tests;

public sealed class WindowsJobBuildProcessRunnerTests : IDisposable
{
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "BuildTree-" + Guid.NewGuid().ToString("N"));
    public WindowsJobBuildProcessRunnerTests() => Directory.CreateDirectory(root);

    [Theory]
    [InlineData("cancel")]
    [InlineData("timeout")]
    [InlineData("nonzero")]
    public async Task RootExitDoesNotLoseInheritedPipeDescendants(string outcome)
    {
        if (!OperatingSystem.IsWindows()) return;
        var recovery = new BuildCleanupRecovery();
        var runner = new WindowsJobBuildProcessRunner(recovery,
            outcome == "timeout" ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5));
        var runs = new RunOrchestrator(new CoopMcpServerSettings(), null, null, null, null, null, buildCleanup: recovery);
        string lockPath = Path.Combine(root, "deployment.lock");
        using var cancellation = new CancellationTokenSource();
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "CoopMcpServer.TestHost.exe"))
        {
            WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string arg in new[] { "build-tree", root, "0", outcome == "nonzero" ? "17" : "0" }) info.ArgumentList.Add(arg);
        var observed = new List<Process>();
        Task<DeploymentReport> build = runs.DeployAsync(async () =>
        {
            using var lease = File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            await runner.RunAsync(info, root, "Coop", cancellation.Token);
            return new DeploymentReport();
        }, default);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            for (int depth = 0; depth < 3; depth++)
            {
                string marker = Path.Combine(root, depth + ".pid");
                while (!File.Exists(marker) || new FileInfo(marker).Length == 0) await Task.Delay(20, deadline.Token);
                var process = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(marker, deadline.Token)));
                _ = process.Handle; // Retain the actual process identity across termination and PID reuse.
                observed.Add(process);
            }
            Assert.Throws<IOException>(() => File.Open(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => runs.DeployAsync(() => Task.FromResult(new DeploymentReport()), default));
            File.WriteAllText(Path.Combine(root, "release-root"), "exit");
            await observed[0].WaitForExitAsync(deadline.Token);
            Assert.Equal(outcome == "nonzero" ? 17 : 0, observed[0].ExitCode);
            if (outcome != "nonzero")
            {
                Assert.False(build.IsCompleted);
                Assert.False(observed[1].HasExited);
                Assert.False(observed[2].HasExited);
            }
            var elapsed = Stopwatch.StartNew();
            if (outcome == "cancel") cancellation.Cancel();
            if (outcome == "nonzero")
                await Assert.ThrowsAsync<InvalidOperationException>(() => build.WaitAsync(TimeSpan.FromSeconds(12)));
            else
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => build.WaitAsync(TimeSpan.FromSeconds(12)));
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(12));
            Assert.All(observed, process => Assert.True(process.HasExited));
            foreach (string stream in new[] { "stdout", "stderr" })
            {
                string log = Path.Combine(root, "Coop-" + stream + ".log");
                using (File.Open(log, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                Assert.Contains(stream + " from depth 2", File.ReadAllText(log));
            }
            await runs.DeployAsync(() =>
            {
                using var lease = File.Open(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return Task.FromResult(new DeploymentReport());
            }, default);
        }
        finally
        {
            cancellation.Cancel();
            try { await build.WaitAsync(TimeSpan.FromSeconds(12)); } catch { }
            foreach (var process in observed)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                process.Dispose();
            }
            await recovery.RecoverAsync();
        }
    }

    public void Dispose() => Directory.Delete(root, true);
}
