using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Common.Session;
using Coop.Core.Server.Connections.Messages;
using GameInterface;
using GameInterface.Services.GameState;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading;

namespace Coop.Core.Server.Services.Session;

/// <summary>
/// Shuts a spawn-managed server down when it is no longer needed: after its last player
/// leaves, when the hosting client that spawned it dies, or when nobody ever connects.
/// Owned by the process (not the session container) so its timers survive a container
/// teardown that would otherwise orphan the spawned game window. Inert on servers that
/// were not spawned by a client.
/// </summary>
public class ManagedServerLifetime : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<ManagedServerLifetime>();

    // Long enough for a briefly disconnected host to come back before the world unloads.
    public static readonly TimeSpan EmptyServerGrace = TimeSpan.FromSeconds(30);

    // A spawned server nobody ever reached was abandoned by its client; bound how long it idles.
    public static readonly TimeSpan FirstConnectTimeout = TimeSpan.FromMinutes(10);

    private readonly IMessageBroker messageBroker;
    private readonly object stateLock = new object();

    private Process ownerProcess;
    private Timer quitTimer;
    private int peerCount;
    private bool quitting;
    private volatile bool disposed;

    public ManagedServerLifetime(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        if (!ManagedServerConfig.IsManagedServer) return;

        messageBroker.Subscribe<PlayerConnected>(Handle_PlayerConnected);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<EndCoopMode>(Handle_EndCoopMode);

        ScheduleQuit(FirstConnectTimeout, "no player connected");
        WatchOwnerProcess();
    }

    public void Dispose()
    {
        if (!ManagedServerConfig.IsManagedServer) return;

        messageBroker.Unsubscribe<PlayerConnected>(Handle_PlayerConnected);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<EndCoopMode>(Handle_EndCoopMode);

        lock (stateLock)
        {
            disposed = true;
            quitTimer?.Dispose();
            quitTimer = null;

            if (ownerProcess != null)
            {
                ownerProcess.Exited -= Handle_OwnerExited;
                ownerProcess.Dispose();
                ownerProcess = null;
            }
        }
    }

    private void WatchOwnerProcess()
    {
        try
        {
            var owner = Process.GetProcessById(ManagedServerConfig.OwnerProcessId);

            // A recycled pid points at some unrelated process started after this one.
            if (owner.StartTime > Process.GetCurrentProcess().StartTime)
            {
                ScheduleQuit(EmptyServerGrace, "owning client is already gone");
                return;
            }

            // Subscribe before enabling events so an exit that lands right now still fires the handler.
            owner.Exited += Handle_OwnerExited;
            owner.EnableRaisingEvents = true;

            lock (stateLock)
            {
                ownerProcess = owner;
            }

            // Exited does not fire retroactively, so cover an exit between GetProcessById and here.
            if (owner.HasExited)
            {
                ScheduleQuit(EmptyServerGrace, "owning client is already gone");
            }
        }
        catch (ArgumentException)
        {
            ScheduleQuit(EmptyServerGrace, "owning client is already gone");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not watch the owning client process; relying on the empty-server shutdown only");
        }
    }

    private void Handle_OwnerExited(object sender, EventArgs e)
    {
        ScheduleQuit(EmptyServerGrace, "owning client exited");
    }

    private void Handle_PlayerConnected(MessagePayload<PlayerConnected> obj)
    {
        lock (stateLock)
        {
            peerCount++;

            if (!quitting)
            {
                quitTimer?.Dispose();
                quitTimer = null;
            }
        }
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> obj)
    {
        lock (stateLock)
        {
            if (peerCount > 0) peerCount--;

            if (peerCount == 0)
            {
                ScheduleQuitLocked(EmptyServerGrace, "last player left");
            }
        }
    }

    // The operator quit the spawned server to the main menu; the session it existed for is over.
    private void Handle_EndCoopMode(MessagePayload<EndCoopMode> obj)
    {
        lock (stateLock)
        {
            if (quitting) return;
            quitting = true;
        }

        // The session is already returning to the menu, so the listener is coming down anyway.
        CommitQuit(stopNetwork: false);
    }

    private void ScheduleQuit(TimeSpan delay, string reason)
    {
        lock (stateLock)
        {
            ScheduleQuitLocked(delay, reason);
        }
    }

    private void ScheduleQuitLocked(TimeSpan delay, string reason)
    {
        // A callback dispatched before Dispose ran its unsubscribe must not re-arm a dead timer.
        if (quitting || disposed) return;

        Logger.Information("Managed server will save and quit in {Delay} ({Reason}) unless a player connects", delay, reason);

        quitTimer?.Dispose();
        quitTimer = new Timer(_ => QuitNow(), null, delay, Timeout.InfiniteTimeSpan);
    }

    private void QuitNow()
    {
        lock (stateLock)
        {
            if (quitting || disposed) return;

            // A player racing the deadline keeps the server alive; the empty-server
            // shutdown re-arms when the last one leaves.
            if (peerCount > 0)
            {
                quitTimer?.Dispose();
                quitTimer = null;
                return;
            }

            quitting = true;
        }

        CommitQuit(stopNetwork: true);
    }

    private void CommitQuit(bool stopNetwork)
    {
        Logger.Information("Saving and shutting down the managed co-op server");

        GameThread.RunSafe(() =>
        {
            // Stop accepting joins first so nobody connects into a process that is about to exit.
            // Isolated from the save so a failed network teardown can't skip persisting the session.
            if (stopNetwork)
            {
                try
                {
                    if (ContainerProvider.TryResolve<INetwork>(out var network)) network.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to stop the server network before shutdown");
                }
            }

            ServerShutdown.SaveAndQuit(ManagedServerConfig.SaveName);
        }, context: "ManagedServerQuit");
    }
}
