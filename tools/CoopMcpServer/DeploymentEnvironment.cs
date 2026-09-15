using System.Diagnostics;

namespace CoopMcpServer;

public interface IDeploymentEnvironment
{
    void RequireIdle();
    void RequireSpace(string path, long bytes);
}

public sealed class DeploymentEnvironment : IDeploymentEnvironment
{
    public void RequireIdle()
    {
        var active = new List<string>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string name;
                try { name = process.ProcessName; }
                catch (InvalidOperationException) { continue; }
                if (name.StartsWith("Bannerlord", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("DedicatedServer", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ServerConsole", StringComparison.OrdinalIgnoreCase))
                    active.Add($"{name} (PID {process.Id})");
            }
        }
        if (active.Count != 0) throw new InvalidOperationException("Stop game/server processes before deploying: " + string.Join(", ", active));
    }

    public void RequireSpace(string path, long bytes)
    {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)));
        if (!drive.IsReady || drive.AvailableFreeSpace < bytes)
            throw new IOException($"Insufficient disk space at {path}: need {bytes} bytes including reserve.");
    }
}

public interface IDeploymentRunGuard
{
    Task<DeploymentReport> DeployAsync(Func<Task<DeploymentReport>> action, CancellationToken cancellationToken);
}

public interface IDeploymentLease
{
    IDisposable Acquire(LaunchProfile profile);
}

// Shared by starts and deployments, including separate MCP processes using the same configured root.
public sealed class DeploymentLease : IDeploymentLease
{
    private readonly IDeploymentPaths paths;
    public DeploymentLease(IDeploymentPaths paths) { this.paths = paths; }

    public IDisposable Acquire(LaunchProfile profile)
    {
        if (profile.Deployment == null) return null;
        string root = paths.DurableRoot(profile.Deployment.DurableRoot);
        Directory.CreateDirectory(root);
        var lease = new FileStream(Path.Combine(root, "deployment.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (File.Exists(Path.Combine(root, "recovery-required.json")))
                throw new InvalidOperationException("Deployment recovery is required. Inspect " + Path.Combine(root, "recovery-required.json") + "; do not start or redeploy until verified recovery.");
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }
}

