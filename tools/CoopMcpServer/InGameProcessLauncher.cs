using System.Diagnostics;

namespace CoopMcpServer;

public interface IOwnedProcess : IDisposable
{
    int Pid { get; }
    DateTime StartedUtc { get; }
    bool IsAlive { get; }
    Task StopAsync(TimeSpan grace);
}

public interface IGameProcessLauncher
{
    IOwnedProcess Launch(LaunchProfile profile, string role, string platformId, string runToken);
}

public sealed class InGameProcessLauncher : IGameProcessLauncher
{
    public IOwnedProcess Launch(LaunchProfile profile, string role, string platformId, string runToken)
    {
        var process = Process.Start(CreateStartInfo(profile, role, platformId, runToken));
        if (process == null) throw new InvalidOperationException("Bannerlord did not start.");
        try
        {
            return new OwnedProcess(process);
        }
        catch
        {
            // Only the process just created by this launcher can be cleaned up here.
            try { if (!process.HasExited) process.Kill(); }
            finally { process.Dispose(); }
            throw;
        }
    }

    public ProcessStartInfo CreateStartInfo(LaunchProfile profile, string role, string platformId, string runToken)
    {
        var info = new ProcessStartInfo(profile.Executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(profile.Executable),
        };
        foreach (string argument in new[] { "/singleplayer", "/" + role, "/autoconnect",
            "/platformId", platformId, "/cooptestrun", runToken })
            info.ArgumentList.Add(argument);
        if (role == "client") info.ArgumentList.Add("/cooptestmanualjoin");
        info.ArgumentList.Add("_MODULES_*" + string.Join("*", profile.Modules) + "*_MODULES_");
        return info;
    }

    private sealed class OwnedProcess : IOwnedProcess
    {
        private readonly Process process;
        public int Pid { get; }
        public DateTime StartedUtc { get; }
        public bool IsAlive => !process.HasExited && process.StartTime.ToUniversalTime() == StartedUtc;

        public OwnedProcess(Process process)
        {
            this.process = process;
            Pid = process.Id;
            StartedUtc = process.StartTime.ToUniversalTime();
        }

        public async Task StopAsync(TimeSpan grace)
        {
            if (!IsAlive) return;
            using var timeout = new CancellationTokenSource(grace);
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { }
            if (!IsAlive) return;
            process.Kill();
            using var killTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(killTimeout.Token);
        }

        public void Dispose() => process.Dispose();
    }
}
