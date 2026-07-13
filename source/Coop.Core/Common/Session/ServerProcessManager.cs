using Common.Logging;
using Common.Messaging;
using Common.Network.Session;
using Coop.Core.Common.Session.Messages;
using GameInterface.Services.GameState;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;

namespace Coop.Core.Common.Session;

/// <summary>
/// Spawns and tracks the dedicated server process a Host click creates. The child remains
/// independent until the user closes it; this manager observes and disowns the process but
/// never terminates it.
/// </summary>
public class ServerProcessManager : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerProcessManager>();

    private readonly IMessageBroker messageBroker;
    private readonly object stateLock = new object();

    private Process serverProcess;

    public ServerProcessManager(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public bool IsRunning
    {
        get
        {
            lock (stateLock)
            {
                return serverProcess != null && !HasExited(serverProcess);
            }
        }
    }

    public void Start(string saveName) => Start(saveName, null, ServerVisibility.Public);

    public void Start(string saveName, string password) => Start(saveName, password, ServerVisibility.Public);

    public void Start(string saveName, string password, ServerVisibility visibility)
    {
        lock (stateLock)
        {
            if (serverProcess != null && !HasExited(serverProcess))
                throw new InvalidOperationException("A hosted server process is already running");

            CleanupLocked();

            var currentProcess = Process.GetCurrentProcess();
            var exePath = ManagedServerLauncher.GetEngineExecutablePath();
            var arguments = ServerLaunchArguments.BuildManagedServerArguments(
                ManagedServerLauncher.GetActiveModuleIds(), saveName, currentProcess.Id, password, visibility);

            // The arguments may contain the hosted-server password, so never write them to a log.
            Logger.Information("Spawning co-op server for save '{SaveName}': {Exe}", saveName, exePath);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = false,
                },
            };

            // Arm the exit watch and record the child as current before Start, so an instantly
            // exiting child still raises Exited and OnServerExited (which takes stateLock) sees it
            // as the current process rather than dropping it as stale.
            process.Exited += OnServerExited;
            process.EnableRaisingEvents = true;
            serverProcess = process;

            try
            {
                process.Start();
            }
            catch
            {
                CleanupLocked();
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (stateLock)
        {
            CleanupLocked();
        }
    }

    private void OnServerExited(object sender, EventArgs e)
    {
        int exitCode = 0;
        try
        {
            exitCode = ((Process)sender).ExitCode;
        }
        catch (Exception)
        {
            // Best effort; the handle may already be disposed.
        }

        Logger.Information("Co-op server process exited with code {ExitCode}", exitCode);

        bool isCurrent;
        lock (stateLock)
        {
            isCurrent = ReferenceEquals(sender, serverProcess);
            if (isCurrent) CleanupLocked();
        }

        // A stale child's exit must not be mistaken for the current one's.
        if (isCurrent)
        {
            messageBroker.Publish(this, new HostedServerExited());
        }
    }

    private void CleanupLocked()
    {
        if (serverProcess != null)
        {
            serverProcess.Exited -= OnServerExited;
            serverProcess.Dispose();
            serverProcess = null;
        }
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
